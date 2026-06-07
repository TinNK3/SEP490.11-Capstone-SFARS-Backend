using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Infrastructure.Configurations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// EfficientNetV2 (ONNX) inference service for 16-class snake species classification.
/// Auto-detects input name, tensor layout (NCHW vs NHWC), and applies softmax if needed.
/// Pixel values are kept in [0, 255] range matching the training pipeline.
/// </summary>
public class EfficientNetClassificationService : ISpeciesClassificationService, IDisposable
{
    private readonly ILogger<EfficientNetClassificationService> _logger;
    private readonly ClassificationModelOptions _options;

    private InferenceSession _speciesSession;
    private readonly ReaderWriterLockSlim _speciesLock = new();

    private Dictionary<int, string> _speciesClassMapping;
    private IReadOnlyList<string>? _cachedSupportedSpecies;

    // Resolved at startup from ONNX metadata
    private string _resolvedInputName = string.Empty;
    private bool _isChannelsLast; // true = NHWC [1,H,W,C], false = NCHW [1,C,H,W]

    public EfficientNetClassificationService(
        ILogger<EfficientNetClassificationService> logger,
        IOptions<ClassificationModelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _speciesSession = new InferenceSession(_options.ModelPath, sessionOptions);

        // Priority: JSON file from retrain pipeline > config string fallback
        var fileMappingPath = ResolveModelRelativePath(_options.ClassMappingPath);
        _speciesClassMapping = LoadClassMappingFromFile(fileMappingPath)
                               ?? ParseClassMapping(_options.ClassMapping);

        _logger.LogInformation("Loaded {Count} species classes", _speciesClassMapping.Count);
        _cachedSupportedSpecies = _speciesClassMapping.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList().AsReadOnly();
        ResolveInputMetadata(_speciesSession);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesPrediction>> InferSpeciesOnlyAsync(Stream imageStream, int topK = 3)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imageStream);

            var speciesTensor = PreprocessImage(image,
                _options.SpeciesInputWidth,
                _options.SpeciesInputHeight,
                _isChannelsLast);

            var speciesInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_resolvedInputName, speciesTensor)
            };

            float[] speciesOutput;

            _speciesLock.EnterReadLock();
            try
            {
                using var speciesResults = _speciesSession.Run(speciesInputs);
                speciesOutput = speciesResults.First().AsEnumerable<float>().ToArray();
            }
            finally
            {
                _speciesLock.ExitReadLock();
            }

            // Apply softmax if the model outputs raw logits (values outside [0,1])
            if (speciesOutput.Any(v => v < 0 || v > 1))
            {
                speciesOutput = Softmax(speciesOutput);
            }

            var predictions = GetPredictionsTopK(speciesOutput, _speciesClassMapping, topK);

            _logger.LogInformation(
                "Species inference completed. Top: {Class} ({Confidence:P})",
                predictions.FirstOrDefault()?.ClassName,
                predictions.FirstOrDefault()?.Confidence);

            return predictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Species-only classification failed");
            throw new InvalidOperationException(
                "AI species classification failed. Please try again.", ex);
        }
    }

    /// <inheritdoc />
    public Task<bool> ReloadSpeciesModelAsync(string? newModelPath = null)
    {
        return Task.Run(() =>
        {
            string pathToLoad = newModelPath ?? _options.ModelPath;

            if (!File.Exists(pathToLoad))
            {
                _logger.LogError("Hot-Swap Failed: Model file not found at {Path}", pathToLoad);
                return false;
            }

            try
            {
                _logger.LogInformation("Initiating Hot-Swap for Species model from {Path}", pathToLoad);

                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                var newSession = new InferenceSession(pathToLoad, sessionOptions);

                // Reload class mapping from file (new model may have different classes)
                var fileMappingPath = ResolveModelRelativePath(_options.ClassMappingPath);
                var newMapping = LoadClassMappingFromFile(fileMappingPath);

                _speciesLock.EnterWriteLock();
                try
                {
                    var oldSession = _speciesSession;
                    _speciesSession = newSession;
                    oldSession?.Dispose();
                    ResolveInputMetadata(newSession);

                    if (newMapping != null)
                    {
                        _speciesClassMapping = newMapping;
                        _cachedSupportedSpecies = _speciesClassMapping.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList().AsReadOnly();
                        _logger.LogInformation("Hot-Swap: Class mapping reloaded with {Count} classes", newMapping.Count);
                    }
                }
                finally
                {
                    _speciesLock.ExitWriteLock();
                }

                _logger.LogInformation("Hot-Swap Successful: New Species model loaded");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hot-Swap Failed: Error loading new model");
                return false;
            }
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedSpecies()
    {
        _speciesLock.EnterReadLock();
        try
        {
            return _cachedSupportedSpecies ?? new List<string>().AsReadOnly();
        }
        finally
        {
            _speciesLock.ExitReadLock();
        }
    }

    // ─────────────────────── Private helpers ───────────────────────

    private void ResolveInputMetadata(InferenceSession session)
    {
        var inputMeta = session.InputMetadata.FirstOrDefault();
        _resolvedInputName = inputMeta.Key ?? _options.SpeciesInputName;

        var dims = inputMeta.Value?.Dimensions;
        if (dims is { Length: 4 })
        {
            _isChannelsLast = dims[3] == 3;
        }
        else
        {
            _isChannelsLast = false;
        }

        _logger.LogInformation(
            "Species model resolved — Input: '{InputName}', Shape: [{Shape}], Layout: {Layout}",
            _resolvedInputName,
            dims != null ? string.Join(", ", dims) : "unknown",
            _isChannelsLast ? "NHWC (channels-last)" : "NCHW (channels-first)");
    }

    /// <summary>
    /// Build float tensor in NCHW or NHWC layout.
    /// Pixel values are kept in [0, 255] range — matching the Python training pipeline
    /// which uses <c>rgb_img.astype(np.float32)</c> without normalization.
    /// </summary>
    private static DenseTensor<float> PreprocessImage(
        Image<Rgb24> originalImage,
        int width,
        int height,
        bool channelsLast)
    {
        // Letterbox: maintain aspect ratio + pad with black
        float scale = Math.Min((float)width / originalImage.Width, (float)height / originalImage.Height);
        int newW = (int)(originalImage.Width * scale);
        int newH = (int)(originalImage.Height * scale);
        int padX = (width - newW) / 2;
        int padY = (height - newH) / 2;

        using var resized = originalImage.Clone(ctx => ctx.Resize(newW, newH));

        var tensor = channelsLast
            ? new DenseTensor<float>(new[] { 1, height, width, 3 })   // NHWC
            : new DenseTensor<float>(new[] { 1, 3, height, width });  // NCHW

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = x - padX;
                int srcY = y - padY;

                float r = 0f, g = 0f, b = 0f;
                if (srcX >= 0 && srcX < newW && srcY >= 0 && srcY < newH)
                {
                    var pixel = resized[srcX, srcY];
                    // Keep 0-255 range — matching Python training: rgb_img.astype(np.float32)
                    r = pixel.R;
                    g = pixel.G;
                    b = pixel.B;
                }

                if (channelsLast)
                {
                    tensor[0, y, x, 0] = r;
                    tensor[0, y, x, 1] = g;
                    tensor[0, y, x, 2] = b;
                }
                else
                {
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            }
        }

        return tensor;
    }

    private static float[] Softmax(float[] logits)
    {
        float max = logits.Max();
        float[] exps = logits.Select(v => MathF.Exp(v - max)).ToArray();
        float sum = exps.Sum();
        return exps.Select(v => v / sum).ToArray();
    }

    private List<SpeciesPrediction> GetPredictionsTopK(float[] output, Dictionary<int, string> mapping, int topK)
    {
        return output
            .Select((confidence, index) => new SpeciesPrediction(
                ClassName: mapping.GetValueOrDefault(index, $"unknown_class_{index}"),
                Confidence: confidence,
                ClassIndex: index
            ))
            .OrderByDescending(p => p.Confidence)
            .Take(topK)
            .ToList();
    }

    private Dictionary<int, string> ParseClassMapping(string mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping))
        {
            _logger.LogWarning("No class mapping provided");
            return new Dictionary<int, string>();
        }

        try
        {
            return mapping
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(':'))
                .ToDictionary(
                    parts => int.Parse(parts[0].Trim()),
                    parts => parts[1].Trim()
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse class mapping: {Mapping}", mapping);
            return new Dictionary<int, string>();
        }
    }

    /// <summary>
    /// Load class mapping from a JSON file generated by the Python retrain pipeline.
    /// Expected format: {"0": "azemiops_feae", "1": "bungarus_candidus", ...}
    /// </summary>
    private Dictionary<int, string>? LoadClassMappingFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null || dict.Count == 0) return null;

            var result = dict.ToDictionary(
                kv => int.Parse(kv.Key),
                kv => kv.Value
            );

            _logger.LogInformation("Loaded class mapping from file: {Path} ({Count} classes)", path, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load class mapping from file: {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Resolve a path that may be relative to the model directory.
    /// </summary>
    private string? ResolveModelRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;

        // Resolve relative to the model's directory
        var modelDir = Path.GetDirectoryName(_options.ModelPath);
        if (!string.IsNullOrEmpty(modelDir))
        {
            var candidate = Path.Combine(modelDir, Path.GetFileName(path));
            if (File.Exists(candidate)) return candidate;
        }

        // Try as-is (relative to CWD)
        return path;
    }

    public void Dispose()
    {
        _speciesLock.EnterWriteLock();
        try
        {
            _speciesSession?.Dispose();
        }
        finally
        {
            _speciesLock.ExitWriteLock();
            _speciesLock.Dispose();
        }
    }
}