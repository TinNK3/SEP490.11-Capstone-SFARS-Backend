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
/// YOLO inference service using cascaded ONNX Runtime models (Snake vs Not Snake -> 15 Species)
/// </summary>
public class YoloInferenceService : IYoloInferenceService, IDisposable
{
    private readonly ILogger<YoloInferenceService> _logger;
    private readonly YoloModelOptions _options;
    
    private readonly InferenceSession _binarySession;
    private readonly InferenceSession _speciesSession;
    
    private readonly Dictionary<int, string> _binaryClassMapping;
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

        // Load Binary ONNX model
        _binarySession = new InferenceSession(_options.BinaryModelPath, sessionOptions);
        _binaryClassMapping = ParseClassMapping(_options.BinaryClassMapping);
        _logger.LogInformation("Binary YOLO model loaded: {ModelPath}", _options.BinaryModelPath);

        // Load Species ONNX model
        _speciesSession = new InferenceSession(_options.ModelPath, sessionOptions);
        _speciesClassMapping = ParseClassMapping(_options.ClassMapping);
        _logger.LogInformation("Species YOLO model loaded: {ModelPath}", _options.ModelPath);
    }

    public async Task<YoloPipelineResult> InferCascadedAsync(Stream imageStream, int topK = 3)
    {
        try
        {
            // 1. Preprocess image ONCE
            var inputTensor = await PreprocessImageAsync(imageStream);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };

            // 2. Binary Inference (Snake vs Not Snake)
            using var binaryResults = _binarySession.Run(inputs);
            var binaryOutput = binaryResults.First().AsEnumerable<float>().ToArray();
            var binaryPredictions = GetPredictionsTopK(binaryOutput, _binaryClassMapping, topK: 1); // Only need top 1
            
            var topBinary = binaryPredictions.FirstOrDefault();
            bool isSnake = topBinary != null && topBinary.ClassName.Equals("snake", StringComparison.OrdinalIgnoreCase);
            
            _logger.LogInformation("Binary inference completed. Top class: {Class} ({Confidence:P})", 
                topBinary?.ClassName, topBinary?.Confidence);

            // 3. Early Exit if Not Snake
            if (!isSnake)
            {
                return new YoloPipelineResult(
                    IsSnake: false,
                    BinaryConfidence: topBinary?.Confidence ?? 0f,
                    SpeciesPredictions: new List<YoloPrediction>()
                );
            }

            // 4. Species Inference (using the EXACT SAME TENSOR)
            using var speciesResults = _speciesSession.Run(inputs); // Re-using `inputs`
            var speciesOutput = speciesResults.First().AsEnumerable<float>().ToArray();
            var speciesPredictions = GetPredictionsTopK(speciesOutput, _speciesClassMapping, topK);

            _logger.LogInformation("Species inference completed. Top prediction: {Class} ({Confidence:P})", 
                speciesPredictions.FirstOrDefault()?.ClassName, 
                speciesPredictions.FirstOrDefault()?.Confidence);

            return new YoloPipelineResult(
                IsSnake: true,
                BinaryConfidence: topBinary?.Confidence ?? 0f,
                SpeciesPredictions: speciesPredictions
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cascaded YOLO inference failed");
            throw new InvalidOperationException("AI inference pipeline failed. Please try again.", ex);
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
        _binarySession?.Dispose();
        _speciesSession?.Dispose();
    }
}