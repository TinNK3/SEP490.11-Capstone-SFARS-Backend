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
/// YOLO inference service using ONNX Runtime for 15 Species classification
/// </summary>
public class YoloInferenceService : IYoloInferenceService, IDisposable
{
    private readonly ILogger<YoloInferenceService> _logger;
    private readonly YoloModelOptions _options;
    
    private InferenceSession _speciesSession;
    private readonly ReaderWriterLockSlim _speciesLock = new ReaderWriterLockSlim();
    
    private readonly Dictionary<int, string> _speciesClassMapping;

    public YoloInferenceService(
        ILogger<YoloInferenceService> logger,
        IOptions<YoloModelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Optimize ONNX Runtime for server deployment
        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        // Load Species ONNX model
        _speciesSession = new InferenceSession(_options.ModelPath, sessionOptions);
        _speciesClassMapping = ParseClassMapping(_options.ClassMapping);
        _logger.LogInformation("Species YOLO model loaded: {ModelPath}", _options.ModelPath);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<YoloPrediction>> InferSpeciesOnlyAsync(Stream imageStream, int topK = 3)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(imageStream);

            var speciesTensor = PreprocessImage(image,
                _options.SpeciesInputWidth,
                _options.SpeciesInputHeight);

            var speciesInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", speciesTensor)
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

            var predictions = GetPredictionsTopK(speciesOutput, _speciesClassMapping, topK);

            _logger.LogInformation(
                "Species-only inference completed. Top prediction: {Class} ({Confidence:P})",
                predictions.FirstOrDefault()?.ClassName,
                predictions.FirstOrDefault()?.Confidence);

            return predictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Species-only YOLO inference failed");

            throw new InvalidOperationException(
                "AI species classification failed. Please try again.",
                ex);
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
                _logger.LogInformation("Initiating Hot-Swap for Species ONNX model from {Path}", pathToLoad);

                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                // Load new session first to ensure it's valid BEFORE acquiring write lock
                var newSession = new InferenceSession(pathToLoad, sessionOptions);

                // Acquire write lock: Blocks all new incoming InferCascadedAsync / InferSpeciesOnlyAsync 
                // Wait for existing inferences to finish, then swap.
                _speciesLock.EnterWriteLock();
                try
                {
                    var oldSession = _speciesSession;
                    _speciesSession = newSession;
                    
                    // Dispose old session safely
                    oldSession?.Dispose();
                }
                finally
                {
                    _speciesLock.ExitWriteLock();
                }

                _logger.LogInformation("Hot-Swap Successful: New Species YOLO model loaded");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hot-Swap Failed: Error loading new ONNX model");
                return false;
            }
        });
    }

    private DenseTensor<float> PreprocessImage(
    Image<Rgb24> originalImage,
    int width,
    int height)
    {
        using var image = originalImage.Clone(ctx => ctx.Resize(width, height));

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];

                tensor[0, 0, y, x] = pixel.R / 255f;
                tensor[0, 1, y, x] = pixel.G / 255f;
                tensor[0, 2, y, x] = pixel.B / 255f;
            }
        }

        return tensor;
    }

    private List<YoloPrediction> GetPredictionsTopK(float[] output, Dictionary<int, string> mapping, int topK)
    {
        // Assuming output is class probabilities
        var predictions = output
            .Select((confidence, index) => new YoloPrediction(
                ClassName: mapping.GetValueOrDefault(index, $"unknown_class_{index}"),
                Confidence: confidence,
                ClassIndex: index
            ))
            .OrderByDescending(p => p.Confidence)
            .Take(topK)
            .ToList();

        return predictions;
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