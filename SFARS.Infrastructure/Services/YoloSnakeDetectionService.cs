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
/// YOLOv8 ONNX-based snake detection service (Stage 1).
/// Detects snake presence and returns bounding box in original image pixel coords.
/// </summary>
public class YoloSnakeDetectionService : ISnakeDetectionService, IDisposable
{
    private readonly ILogger<YoloSnakeDetectionService> _logger;
    private readonly YoloDetectionOptions _options;

    private InferenceSession _session;
    private readonly ReaderWriterLockSlim _sessionLock = new();

    private string _resolvedInputName;

    public YoloSnakeDetectionService(
        ILogger<YoloSnakeDetectionService> logger,
        IOptions<YoloDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _session = new InferenceSession(_options.ModelPath, sessionOptions);
        _resolvedInputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "images";

        _logger.LogInformation(
            "YOLO detection model loaded: {ModelPath}, Input: '{InputName}'",
            _options.ModelPath, _resolvedInputName);
    }

    /// <inheritdoc />
    public async Task<SnakeDetectionResult> DetectAsync(byte[] imageBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var image = Image.Load<Rgb24>(imageBytes);
                int origW = image.Width;
                int origH = image.Height;
                int inputSize = _options.InputSize;

                // Letterbox resize: maintain aspect ratio + pad
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
                    NamedOnnxValue.CreateFromTensor(_resolvedInputName, tensor)
                };

                float[] outputData;
                int[] outputDims;

                _sessionLock.EnterReadLock();
                try
                {
                    using var results = _session.Run(inputs);
                    var firstOutput = results.First();
                    outputData = firstOutput.AsEnumerable<float>().ToArray();
                    outputDims = firstOutput.AsTensor<float>().Dimensions.ToArray();
                }
                finally
                {
                    _sessionLock.ExitReadLock();
                }

                // YOLOv8 output: [1, 5, N] where 5 = [x_center, y_center, w, h, confidence]
                // For single-class detection: [1, 5, 8400]
                var detections = DecodeYoloOutput(outputData, outputDims, inputSize);

                // Apply NMS
                var nmsDetections = ApplyNms(detections, _options.NmsIouThreshold);

                if (nmsDetections.Count == 0)
                {
                    _logger.LogInformation("YOLO: No snake detected in image");
                    return new SnakeDetectionResult(false, 0f, null);
                }

                // Take highest confidence detection
                var best = nmsDetections[0];

                // Convert from letterbox coords back to original image coords
                int xMin = (int)((best.XCenter - best.Width / 2 - padX) / scale);
                int yMin = (int)((best.YCenter - best.Height / 2 - padY) / scale);
                int xMax = (int)((best.XCenter + best.Width / 2 - padX) / scale);
                int yMax = (int)((best.YCenter + best.Height / 2 - padY) / scale);

                // Clamp to image bounds
                xMin = Math.Max(0, xMin);
                yMin = Math.Max(0, yMin);
                xMax = Math.Min(origW, xMax);
                yMax = Math.Min(origH, yMax);

                var box = new BoundingBox(xMin, yMin, xMax, yMax);

                _logger.LogInformation(
                    "YOLO: Snake detected with confidence {Confidence:P}, Box: ({X1},{Y1})-({X2},{Y2})",
                    best.Confidence, xMin, yMin, xMax, yMax);

                return new SnakeDetectionResult(
                    best.Confidence >= _options.ConfidenceThreshold,
                    best.Confidence,
                    box);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YOLO snake detection failed");
                throw new InvalidOperationException("Snake detection failed. Please try again.", ex);
            }
        });
    }

    /// <inheritdoc />
    public Task<bool> ReloadModelAsync(string? newModelPath = null)
    {
        return Task.Run(() =>
        {
            string pathToLoad = newModelPath ?? _options.ModelPath;
            if (!File.Exists(pathToLoad))
            {
                _logger.LogError("YOLO Hot-Swap Failed: File not found at {Path}", pathToLoad);
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

                _sessionLock.EnterWriteLock();
                try
                {
                    var oldSession = _session;
                    _session = newSession;
                    _resolvedInputName = newInputName;
                    oldSession?.Dispose();
                }
                finally
                {
                    _sessionLock.ExitWriteLock();
                }

                _logger.LogInformation("YOLO Hot-Swap Successful: {Path}", pathToLoad);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YOLO Hot-Swap Failed");
                return false;
            }
        });
    }

    // ─────────────────────── YOLO Post-Processing ───────────────────────

    /// <summary>
    /// Decode YOLOv8 raw output tensor into detection list.
    /// YOLOv8 output shape: [1, (4+num_classes), num_anchors] — transposed format.
    /// </summary>
    private List<RawDetection> DecodeYoloOutput(float[] data, int[] dims, int inputSize)
    {
        var results = new List<RawDetection>();

        // YOLOv8: [1, num_features, num_anchors]
        // For single-class: num_features = 5 (x, y, w, h, cls_conf)
        // For multi-class: num_features = 4 + num_classes
        int numFeatures = dims[1]; // e.g., 5 for single-class
        int numAnchors = dims[2];  // e.g., 8400
        int numClasses = numFeatures - 4;

        for (int i = 0; i < numAnchors; i++)
        {
            // Data is in transposed layout: [feature_idx * numAnchors + anchor_idx]
            float cx = data[0 * numAnchors + i];
            float cy = data[1 * numAnchors + i];
            float w  = data[2 * numAnchors + i];
            float h  = data[3 * numAnchors + i];

            // Find max class confidence
            float maxConf = float.MinValue;
            for (int c = 0; c < numClasses; c++)
            {
                float conf = data[(4 + c) * numAnchors + i];
                if (conf > maxConf) maxConf = conf;
            }

            if (maxConf >= _options.ConfidenceThreshold)
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

    private record RawDetection(float XCenter, float YCenter, float Width, float Height, float Confidence);

    public void Dispose()
    {
        _sessionLock.EnterWriteLock();
        try
        {
            _session?.Dispose();
        }
        finally
        {
            _sessionLock.ExitWriteLock();
            _sessionLock.Dispose();
        }
    }
}