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
/// Senior-grade 2-Stage Wound Analysis Pipeline.
/// Stage 1: YOLOv8n ONNX detection — locate wound bounding box.
/// Stage 2: YOLOv8n-cls ONNX classification — Snake Bite vs Non Snake Bite.
/// 
/// Architecture mirrors YoloSnakeDetectionService + EfficientNetClassificationService.
/// Features: Thread-safe sessions, ReaderWriterLockSlim for hot-swap, YOLO post-processing (NMS).
/// </summary>
public class WoundDetectionService : IWoundDetectionService, IDisposable
{
    private readonly ILogger<WoundDetectionService> _logger;
    private readonly WoundDetectionOptions _options;

    // Stage 1: Detection Session
    private InferenceSession _detectionSession;
    private readonly ReaderWriterLockSlim _detectionLock = new();
    private string _detectionInputName;

    // Stage 2: Classification Session
    private InferenceSession _classificationSession;
    private readonly ReaderWriterLockSlim _classificationLock = new();
    private string _classificationInputName;

    /// <summary>
    /// YOLOv8n-cls class order is alphabetical from ImageFolder:
    /// Index 0 = "Non Snake Bite", Index 1 = "Snake Bite"
    /// </summary>
    private const int SnakeBiteClassIndex = 1;

    public WoundDetectionService(
        ILogger<WoundDetectionService> logger,
        IOptions<WoundDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        // Initialize detection session
        _detectionSession = new InferenceSession(_options.DetectionModelPath, sessionOptions);
        _detectionInputName = _detectionSession.InputMetadata.Keys.FirstOrDefault() ?? "images";

        // Initialize classification session
        _classificationSession = new InferenceSession(_options.ClassificationModelPath, sessionOptions);
        _classificationInputName = _classificationSession.InputMetadata.Keys.FirstOrDefault() ?? "images";

        _logger.LogInformation(
            "Wound Pipeline initialized — Detection: '{DetPath}' (input: '{DetInput}'), Classification: '{ClsPath}' (input: '{ClsInput}')",
            _options.DetectionModelPath, _detectionInputName,
            _options.ClassificationModelPath, _classificationInputName);
    }

    //  Stage 1: Wound Detection (YOLOv8n)
    /// <inheritdoc />
    public async Task<WoundDetectionResult> DetectWoundAsync(byte[] imageBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
            using var image = Image.Load<Rgb24>(imageBytes);
            int origW = image.Width;
            int origH = image.Height;
            int inputSize = _options.DetectionInputSize;

            // Letterbox resize: maintain aspect ratio + pad with black
            float scale = Math.Min((float)inputSize / origW, (float)inputSize / origH);
            int newW = (int)(origW * scale);
            int newH = (int)(origH * scale);
            int padX = (inputSize - newW) / 2;
            int padY = (inputSize - newH) / 2;

            using var resized = image.Clone(ctx => ctx.Resize(newW, newH));

            // Build NCHW tensor [1, 3, 640, 640] normalized to [0, 1]
            var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });

            for (int y = 0; y < inputSize; y++)
            {
                for (int x = 0; x < inputSize; x++)
                {
                    int srcX = x - padX;
                    int srcY = y - padY;

                    if (srcX >= 0 && srcX < newW && srcY >= 0 && srcY < newH)
                    {
                        var pixel = resized[srcX, srcY];
                        tensor[0, 0, y, x] = pixel.R / 255f;
                        tensor[0, 1, y, x] = pixel.G / 255f;
                        tensor[0, 2, y, x] = pixel.B / 255f;
                    }
                    // else: stays 0 (black padding)
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_detectionInputName, tensor)
            };

            float[] outputData;
            int[] outputDims;

            _detectionLock.EnterReadLock();
            try
            {
                using var results = _detectionSession.Run(inputs);
                var firstOutput = results.First();
                outputData = firstOutput.AsEnumerable<float>().ToArray();
                outputDims = firstOutput.AsTensor<float>().Dimensions.ToArray();
            }
            finally
            {
                _detectionLock.ExitReadLock();
            }

            // YOLOv8 output: [1, (4+num_classes), num_anchors]
            var detections = DecodeYoloOutput(outputData, outputDims);
            var nmsDetections = ApplyNms(detections, _options.DetectionNmsIouThreshold);

            if (nmsDetections.Count == 0)
            {
                _logger.LogInformation("Wound Detection: No wound detected in image");
                return new WoundDetectionResult(false, 0f, null);
            }

            // Take highest confidence detection
            var best = nmsDetections[0];

            // Convert from letterbox coords back to original image coords
            int xMin = Math.Max(0, (int)((best.XCenter - best.Width / 2 - padX) / scale));
            int yMin = Math.Max(0, (int)((best.YCenter - best.Height / 2 - padY) / scale));
            int xMax = Math.Min(origW, (int)((best.XCenter + best.Width / 2 - padX) / scale));
            int yMax = Math.Min(origH, (int)((best.YCenter + best.Height / 2 - padY) / scale));

            var box = new BoundingBox(xMin, yMin, xMax, yMax);

            _logger.LogInformation(
                "Wound Detection: Wound detected with confidence {Confidence:P}, Box: ({X1},{Y1})-({X2},{Y2})",
                best.Confidence, xMin, yMin, xMax, yMax);

            return new WoundDetectionResult(
                best.Confidence >= _options.DetectionConfidenceThreshold,
                best.Confidence,
                box);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wound detection failed");
            throw new InvalidOperationException("Wound detection failed. Please try again.", ex);
        }
        });
    }

    //  Stage 2: Wound Classification (YOLOv8n-cls)
    /// <inheritdoc />
    public async Task<WoundClassificationResult> ClassifyWoundAsync(byte[] croppedImageBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
            using var image = Image.Load<Rgb24>(croppedImageBytes);
            int inputSize = _options.ClassificationInputSize;

            // Resize to model input size — YOLOv8n-cls uses simple resize (no letterbox)
            using var resized = image.Clone(ctx => ctx.Resize(inputSize, inputSize));

            // Build NCHW tensor [1, 3, 224, 224] normalized to [0, 1]
            // YOLOv8 cls uses [0,1] normalization, NOT ImageNet Mean/Std
            var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });

            for (int y = 0; y < inputSize; y++)
            {
                for (int x = 0; x < inputSize; x++)
                {
                    var pixel = resized[x, y];
                    tensor[0, 0, y, x] = pixel.R / 255f;
                    tensor[0, 1, y, x] = pixel.G / 255f;
                    tensor[0, 2, y, x] = pixel.B / 255f;
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_classificationInputName, tensor)
            };

            float[] outputData;

            _classificationLock.EnterReadLock();
            try
            {
                using var results = _classificationSession.Run(inputs);
                outputData = results.First().AsEnumerable<float>().ToArray();
            }
            finally
            {
                _classificationLock.ExitReadLock();
            }

            // Apply softmax if raw logits (values outside [0,1])
            if (outputData.Any(v => v < 0 || v > 1))
            {
                outputData = Softmax(outputData);
            }

            // Class order (alphabetical from ImageFolder):
            // Index 0 = "Non Snake Bite", Index 1 = "Snake Bite"
            float snakeBiteProb = outputData.Length > SnakeBiteClassIndex
                ? outputData[SnakeBiteClassIndex]
                : 0f;

            bool isSnakeBite = snakeBiteProb >= _options.ClassificationThreshold;
            float reportedConfidence = isSnakeBite ? snakeBiteProb : (1f - snakeBiteProb);

            _logger.LogInformation(
                "Wound Classification: IsSnakeBite={IsSnakeBite}, SnakeBiteProb={Prob:P2}, ReportedConfidence={Conf:P2}",
                isSnakeBite, snakeBiteProb, reportedConfidence);

            return new WoundClassificationResult(isSnakeBite, reportedConfidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wound classification failed");
            throw new InvalidOperationException("Wound classification failed. Please try again.", ex);
        }
        });
    }

    //  Hot-Swap: Zero-Downtime Model Reload
    /// <inheritdoc />
    public Task<bool> ReloadDetectionModelAsync(string? newModelPath = null)
    {
        return Task.Run(() =>
        {
            string pathToLoad = newModelPath ?? _options.DetectionModelPath;
            if (!File.Exists(pathToLoad))
            {
                _logger.LogError("Wound Detection Hot-Swap Failed: File not found at {Path}", pathToLoad);
                return false;
            }

            try
            {
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                var newSession = new InferenceSession(pathToLoad, sessionOptions);
                var newInputName = newSession.InputMetadata.Keys.FirstOrDefault() ?? "images";

                _detectionLock.EnterWriteLock();
                try
                {
                    var oldSession = _detectionSession;
                    _detectionSession = newSession;
                    _detectionInputName = newInputName;
                    oldSession?.Dispose();
                }
                finally
                {
                    _detectionLock.ExitWriteLock();
                }

                _logger.LogInformation("Wound Detection Hot-Swap Successful: {Path}", pathToLoad);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wound Detection Hot-Swap Failed");
                return false;
            }
        });
    }

    /// <inheritdoc />
    public Task<bool> ReloadClassificationModelAsync(string? newModelPath = null)
    {
        return Task.Run(() =>
        {
            string pathToLoad = newModelPath ?? _options.ClassificationModelPath;
            if (!File.Exists(pathToLoad))
            {
                _logger.LogError("Wound Classification Hot-Swap Failed: File not found at {Path}", pathToLoad);
                return false;
            }

            try
            {
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                var newSession = new InferenceSession(pathToLoad, sessionOptions);
                var newInputName = newSession.InputMetadata.Keys.FirstOrDefault() ?? "images";

                _classificationLock.EnterWriteLock();
                try
                {
                    var oldSession = _classificationSession;
                    _classificationSession = newSession;
                    _classificationInputName = newInputName;
                    oldSession?.Dispose();
                }
                finally
                {
                    _classificationLock.ExitWriteLock();
                }

                _logger.LogInformation("Wound Classification Hot-Swap Successful: {Path}", pathToLoad);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wound Classification Hot-Swap Failed");
                return false;
            }
        });
    }

    //  YOLO Post-Processing (shared with detection stage)
    /// <summary>
    /// Decode YOLOv8 raw output tensor into detection list.
    /// YOLOv8 output shape: [1, (4+num_classes), num_anchors] — transposed format.
    /// </summary>
    private List<RawDetection> DecodeYoloOutput(float[] data, int[] dims)
    {
        var results = new List<RawDetection>();

        int numFeatures = dims[1]; // 4 + num_classes (single-class = 5)
        int numAnchors = dims[2];  // e.g. 8400
        int numClasses = numFeatures - 4;

        for (int i = 0; i < numAnchors; i++)
        {
            float cx = data[0 * numAnchors + i];
            float cy = data[1 * numAnchors + i];
            float w = data[2 * numAnchors + i];
            float h = data[3 * numAnchors + i];

            // Find max class confidence
            float maxConf = float.MinValue;
            for (int c = 0; c < numClasses; c++)
            {
                float conf = data[(4 + c) * numAnchors + i];
                if (conf > maxConf) maxConf = conf;
            }

            if (maxConf >= _options.DetectionConfidenceThreshold)
            {
                results.Add(new RawDetection(cx, cy, w, h, maxConf));
            }
        }

        // Sort by confidence descending
        results.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return results;
    }

    /// <summary>
    /// Non-Maximum Suppression to remove overlapping boxes.
    /// </summary>
    private static List<RawDetection> ApplyNms(List<RawDetection> detections, float iouThreshold)
    {
        var kept = new List<RawDetection>();

        foreach (var det in detections)
        {
            bool suppressed = false;
            foreach (var k in kept)
            {
                if (ComputeIou(det, k) > iouThreshold)
                {
                    suppressed = true;
                    break;
                }
            }

            if (!suppressed)
                kept.Add(det);
        }

        return kept;
    }

    private static float ComputeIou(RawDetection a, RawDetection b)
    {
        float ax1 = a.XCenter - a.Width / 2, ay1 = a.YCenter - a.Height / 2;
        float ax2 = a.XCenter + a.Width / 2, ay2 = a.YCenter + a.Height / 2;
        float bx1 = b.XCenter - b.Width / 2, by1 = b.YCenter - b.Height / 2;
        float bx2 = b.XCenter + b.Width / 2, by2 = b.YCenter + b.Height / 2;

        float interX1 = Math.Max(ax1, bx1), interY1 = Math.Max(ay1, by1);
        float interX2 = Math.Min(ax2, bx2), interY2 = Math.Min(ay2, by2);

        float interW = Math.Max(0, interX2 - interX1);
        float interH = Math.Max(0, interY2 - interY1);
        float interArea = interW * interH;

        float areaA = (ax2 - ax1) * (ay2 - ay1);
        float areaB = (bx2 - bx1) * (by2 - by1);

        return interArea / (areaA + areaB - interArea + 1e-6f);
    }

    private static float[] Softmax(float[] logits)
    {
        float max = logits.Max();
        float[] exps = logits.Select(v => MathF.Exp(v - max)).ToArray();
        float sum = exps.Sum();
        return exps.Select(v => v / sum).ToArray();
    }

    private record RawDetection(float XCenter, float YCenter, float Width, float Height, float Confidence);

    //  Dispose
    public void Dispose()
    {
        _detectionLock.EnterWriteLock();
        try
        {
            _detectionSession?.Dispose();
        }
        finally
        {
            _detectionLock.ExitWriteLock();
            _detectionLock.Dispose();
        }

        _classificationLock.EnterWriteLock();
        try
        {
            _classificationSession?.Dispose();
        }
        finally
        {
            _classificationLock.ExitWriteLock();
            _classificationLock.Dispose();
        }
    }
}