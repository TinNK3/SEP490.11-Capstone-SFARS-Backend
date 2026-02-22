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
/// YOLO inference service using ONNX Runtime for snake detection
/// </summary>
public class YoloInferenceService : IYoloInferenceService, IDisposable
{
    private readonly ILogger<YoloInferenceService> _logger;
    private readonly YoloModelOptions _options;
    private readonly InferenceSession _session;
    private readonly Dictionary<int, string> _classMapping;

    public YoloInferenceService(
        ILogger<YoloInferenceService> logger,
        IOptions<YoloModelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Load ONNX model
        _session = new InferenceSession(_options.ModelPath);
        
        // Parse class mapping: "0:naja_kaouthia,1:ophiophagus_hannah"
        _classMapping = ParseClassMapping(_options.ClassMapping);
        
        _logger.LogInformation("YOLO model loaded: {ModelPath}", _options.ModelPath);
    }

    public async Task<IReadOnlyList<YoloPrediction>> InferAsync(Stream imageStream, int topK = 3)
    {
        try
        {
            // Preprocess image
            var inputTensor = await PreprocessImageAsync(imageStream);

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();

            // Get top-K predictions
            var predictions = GetTopKPredictions(output, topK);
            
            _logger.LogInformation("Inference completed. Top prediction: {Class} ({Confidence:P})", 
                predictions.FirstOrDefault()?.ClassName, 
                predictions.FirstOrDefault()?.Confidence);

            return predictions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YOLO inference failed");
            throw new InvalidOperationException("AI inference failed. Please try again.", ex);
        }
    }

    private async Task<DenseTensor<float>> PreprocessImageAsync(Stream imageStream)
    {
        using var image = await Image.LoadAsync<Rgb24>(imageStream);
        
        // Resize to model input size
        image.Mutate(x => x.Resize(_options.InputWidth, _options.InputHeight));

        // Convert to tensor (NCHW format: Batch, Channels, Height, Width)
        var tensor = new DenseTensor<float>(new[] { 1, 3, _options.InputHeight, _options.InputWidth });

        for (int y = 0; y < _options.InputHeight; y++)
        {
            for (int x = 0; x < _options.InputWidth; x++)
            {
                var pixel = image[x, y];
                
                // Normalize to [0, 1]
                tensor[0, 0, y, x] = pixel.R / 255f;
                tensor[0, 1, y, x] = pixel.G / 255f;
                tensor[0, 2, y, x] = pixel.B / 255f;
            }
        }

        return tensor;
    }

    private List<YoloPrediction> GetTopKPredictions(float[] output, int topK)
    {
        // Assuming output is class probabilities
        var predictions = output
            .Select((confidence, index) => new YoloPrediction(
                ClassName: _classMapping.GetValueOrDefault(index, $"unknown_class_{index}"),
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
        _session?.Dispose();
    }
}