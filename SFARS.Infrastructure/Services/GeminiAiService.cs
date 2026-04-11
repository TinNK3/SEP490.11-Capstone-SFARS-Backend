using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Infrastructure.Configurations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SFARS.Infrastructure.Services;

/// <summary>
/// Gemini AI service for enriching snake detection results with first aid advice
/// </summary>
public class GeminiAiService : IGeminiAiService
{
    private readonly ILogger<GeminiAiService> _logger;
    private readonly GeminiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public string ModelName => _options.Model;

    public GeminiAiService(
        ILogger<GeminiAiService> logger,
        IOptions<GeminiOptions> options,
        HttpClient httpClient,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    #region Resilience & Key Rotation

    /// <summary>
    /// Executes an API call with automatic DB-driven key rotation on 429 (Too Many Requests) or 403 (Quota).
    /// </summary>
    private async Task<string> ExecuteWithRotationAsync(Func<string, Task<HttpResponseMessage>> callFunc)
    {
        using var scope = _scopeFactory.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<IGeminiApiKeyService>();

        int maxAttempts = 5; // Prevent hard infinite loops

        for (int i = 0; i < maxAttempts; i++)
        {
            var activeKey = await keyService.AcquireNextAvailableKeyAsync();
            
            if (activeKey == null)
            {
                _logger.LogCritical("No active Gemini API keys available in the database.");
                throw new InvalidOperationException("Gemini API failed: No active/available API keys.");
            }

            try
            {
                var response = await callFunc(activeKey.KeyValue);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();

                    // 400 Bad Request (Usually payload error - stop rotating)
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        _logger.LogError("Gemini 400 Bad Request. Payload Issue. Google Details: {Error}", errorBody);
                        throw new HttpRequestException($"Gemini API Error 400: {errorBody}", null, System.Net.HttpStatusCode.BadRequest);
                    }

                    // 404 Not Found (Usually Model/URL issue - stop rotating)
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogError("Gemini 404 Not Found. Model/URL Issue. Google Details: {Error}", errorBody);
                        throw new HttpRequestException($"Gemini API Error 404: {errorBody}", null, System.Net.HttpStatusCode.NotFound);
                    }

                    // 429: Too Many Requests / 403: Forbidden (Quota/Project Blocked) -> Rotate
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                        errorBody.Contains("quota", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Key {KeyId} quota exceeded or forbidden. Status: {Status}", activeKey.Id, response.StatusCode);
                        await keyService.MarkKeyExhaustedAsync(activeKey.Id);
                        continue;
                    }

                    // Other status codes (404, 5xx) - allow EnsureSuccessStatusCode to throw and maybe catch for rotation if transient
                    response.EnsureSuccessStatusCode();
                }

                // Success
                await keyService.MarkKeySuccessAsync(activeKey.Id);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex) when (i < maxAttempts - 1 && 
                ex.StatusCode != System.Net.HttpStatusCode.BadRequest && 
                ex.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(ex, "API call failed (Transient - Key {KeyId}). Attempting rotation.", activeKey.Id);
                await keyService.MarkKeyExhaustedAsync(activeKey.Id);
                await Task.Delay(500); 
            }
        }

        throw new InvalidOperationException("Gemini API failed after exhausting all attempts with available API keys.");
    }

    #endregion

    #region Snake Detection (Gemini Vision)

    private const string SnakeDetectionSystemPrompt =
        """
        You are an expert wildlife detection system for a medical first-aid application. Your strict task is to locate a REAL snake in the provided image.

        CRITICAL RULES:

        Verification: Confirm the object is an actual biological snake. Strictly IGNORE snake-like objects (e.g., ropes, hoses, sticks, roots, toys). If NO real snake is detected, you MUST return {"box_2d": []}.

        Maximal Visible Extent (Completeness): The bounding box MUST encompass the ENTIRE visible portion of the snake within the camera frame. Whether the snake is fully visible, partially hidden by objects, coiled, or cut off by the image edges (e.g., only the body or head is captured), the box must include ALL visible snake parts present in the photo. Do NOT crop out any visible scales or segments.

        Format: Return ONLY a valid JSON object with the bounding box coordinates [ymin, xmin, ymax, xmax] normalized to the [0, 1000] scale. Absolutely NO markdown, NO text, NO explanations.
        """;

    /// <inheritdoc />
    public async Task<GeminiSnakeDetectionResult> DetectSnakeInImageAsync(byte[] imageBytes, string mimeType)
    {
        var base64Image = Convert.ToBase64String(imageBytes);

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = SnakeDetectionSystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Hình ảnh này có chứa rắn không? Trả lời bằng JSON." },
                        new
                        {
                            inlineData = new
                            {
                                mimeType,
                                data = base64Image
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 256
            }
        };

        var jsonBody = JsonSerializer.Serialize(body);

        string responseContent = await ExecuteWithRotationAsync(async (key) =>
        {
            var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={key}";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            return await _httpClient.SendAsync(request);
        });

        var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
        var resultText = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(resultText))
        {
            throw new InvalidOperationException("Gemini returned empty response for snake detection.");
        }

        return ParseSnakeDetectionResponse(resultText);
    }

    private GeminiSnakeDetectionResult ParseSnakeDetectionResponse(string text)
    {
        var jsonText = StripMarkdownCodeFence(text);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<SnakeDetectionJsonResponse>(jsonText, options);

            if (parsed == null)
            {
                throw new InvalidOperationException($"Failed to parse snake detection JSON: {jsonText}");
            }

            bool isSnake = parsed.Box2D != null && parsed.Box2D.Count == 4;
            return new GeminiSnakeDetectionResult(
                IsSnake: isSnake,
                Box2D: isSnake ? parsed.Box2D : null
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini snake box detection response: {Text}", jsonText);
            return new GeminiSnakeDetectionResult(IsSnake: true, Box2D: new List<int> { 0, 0, 1000, 1000 });
        }
    }

    private sealed class SnakeDetectionJsonResponse
    {
        [JsonPropertyName("box_2d")]
        public List<int>? Box2D { get; set; }
    }

    #endregion

    #region Audio Transcription (Voice Symptom STT)

    private const string AudioTranscriptionSystemPrompt =
        """
        Bạn là bác sĩ cấp cứu chuyên nghiệp và người hiệu đính ngôn ngữ, đang phân tích ghi âm của bệnh nhân báo cáo về tình trạng sơ cứu / rắn cắn.
        LƯU Ý QUAN TRỌNG VỀ GIỌNG NÓI: Bệnh nhân có thể nói giọng địa phương (Bắc, Trung, Nam), nói ngọng, nói nhanh hoặc hoảng loạn dẫn đến âm sắc bị sai lệch (VD: "trẻ nhiều quá" -> "chảy nhiều quá", "chóng mặt nến" -> "chóng mặt lắm", "tức thầy" -> "tức thì", "chảy tưa" -> "chảy máu", "đau nhức hột" -> "đau nhức buốt").
        Bạn BẮT BUỘC phải sử dụng suy luận ngữ cảnh y khoa và cấp cứu để khôi phục lại văn bản đúng chính tả tiếng Việt phổ thông, tuyệt đối KHÔNG được dịch máy móc theo âm thanh.

        Nhiệm vụ: Chuyển đổi giọng nói thành văn bản nguyên thủy đã được hiệu đính (Transcript), đồng thời trích xuất thời gian bị cắn (ra số phút) và các triệu chứng gặp phải.
        
        BẮT BUỘC TRẢ VỀ ĐÚNG MỘT JSON OBJECT CÓ CẤU TRÚC SAU (TUYỆT ĐỐI KHÔNG DÙNG ARRAY, KHÔNG KÈM TEXT GIẢI THÍCH):
        {
            "transcript": "<Lời thoại đã hiệu đính cho có nghĩa. Nếu ồn/trống thì để \"\">",
            "minutes_since_bite": <Số nguyên chỉ phút (VD: 0, 30). Nếu KHÔNG nói rõ thời gian, BẮT BUỘC để null. Không tự đoán mò.>,
            "symptoms": ["<triệu chứng 1>", "<triệu chứng 2>"] // Mảng chữ, rỗng nếu không có
        }
        """;

    public async Task<AudioExtractionResult?> ExtractAudioSymptomsAsync(byte[] audioBytes, string mimeType)
    {
        var base64Audio = Convert.ToBase64String(audioBytes);

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = AudioTranscriptionSystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Phân tích file ghi âm khẩn cấp này thành JSON." },
                        new
                        {
                            inlineData = new
                            {
                                mimeType = mimeType,
                                data = base64Audio
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 800,
                responseMimeType = "application/json"
            }
        };

        var jsonBody = JsonSerializer.Serialize(body);

        try 
        {
            string responseContent = await ExecuteWithRotationAsync(async (key) =>
            {
                var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={key}";
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                return await _httpClient.SendAsync(request);
            });

            var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
            var resultText = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;

            if (string.IsNullOrWhiteSpace(resultText)) return null;

            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var jsonText = StripMarkdownCodeFence(resultText);
            var parsed = JsonSerializer.Deserialize<VoiceExtractionJsonResponse>(jsonText, options);

            if (parsed == null) return null;

            return new AudioExtractionResult(
                Transcript: string.IsNullOrWhiteSpace(parsed.Transcript) ? null : parsed.Transcript.Trim(),
                MinutesSinceBite: parsed.MinutesSinceBite,
                Symptoms: parsed.Symptoms ?? new List<string>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini STT failed after all retries.");
            return null;
        }
    }

    private sealed class VoiceExtractionJsonResponse
    {
        [JsonPropertyName("transcript")]
        public string? Transcript { get; set; }

        [JsonPropertyName("minutes_since_bite")]
        public int? MinutesSinceBite { get; set; }

        [JsonPropertyName("symptoms")]
        public List<string>? Symptoms { get; set; }
    }

    #endregion

    #region Snake Bite Analysis

    public async Task<GeminiAnalysisResult> AnalyzeSnakeBiteAsync(GeminiAnalysisRequest request)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPayload = BuildUserPayload(request);
        var jsonBody = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = JsonSerializer.Serialize(userPayload) } } } },
            generationConfig = new { temperature = _options.Temperature, maxOutputTokens = 1024 }
        });

        try
        {
            string responseContent = await ExecuteWithRotationAsync(async (key) =>
            {
                var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={key}";
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                return await _httpClient.SendAsync(req);
            });

            var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
            var resultText = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;
            
            if (string.IsNullOrEmpty(resultText)) throw new InvalidOperationException("Empty AI response.");

            var jsonText = StripMarkdownCodeFence(resultText);
            var result = JsonSerializer.Deserialize<GeminiAnalysisResult>(jsonText);
            
            return result ?? throw new InvalidOperationException("Failed to parse analysis result.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Analysis failed.");
            return new GeminiAnalysisResult(
                DangerSummary: $"⚠️ {GetDangerLevel(request.ToxicityLevel)} - {request.ToxinGroup}",
                FirstAidSteps: GetFallbackFirstAid(),
                AiNote: "Không thể kết nối AI. Vui lòng đến bệnh viện GẤP để xác định chính xác."
            );
        }
    }

    private string BuildSystemPrompt()
    {
        return @"Bạn là chuyên gia cấp cứu rắn cắn. Nhiệm vụ:
1. Đánh giá mức độ nguy hiểm dựa trên ToxicityLevel và ToxinGroup
2. Tạo DangerSummary ngắn gọn với emoji (⚠️🚑💀)
3. Tạo danh sách FirstAidSteps:
   - Tối đa 5 bước
   - Mỗi bước: Title ngắn (có emoji) + Content (1 câu, max 15 từ)
   - Ưu tiên: Gọi cấp cứu → Giữ tĩnh tại → Rửa vết thương → Không làm gì thêm
4. AiNote: Lời khuyên bổ sung (1-2 câu)

Response format (JSON):
{
  ""DangerSummary"": ""⚠️ description"",
  ""FirstAidSteps"": [
    {""StepOrder"": 1, ""Title"": ""🚑 emoji + short"", ""Content"": ""actionable instruction""}
  ],
  ""AiNote"": ""warning or context""
}";
    }

    private object BuildUserPayload(GeminiAnalysisRequest request)
    {
        return new
        {
            DetectedSnake = new
            {
                CommonName = request.PrimarySnakeName,
                ScientificName = request.PrimarySnakeScientificName,
                Confidence = request.Confidence,
                ToxicityLevel = request.ToxicityLevel,
                ToxinGroup = request.ToxinGroup
            },
            OtherPossibleSnakes = request.OtherCandidates,
            Language = "Vietnamese",
            UserContext = "Nạn nhân đang hoảng loạn, cần hướng dẫn NGẮN GỌN, DỄ HIỂU, CẤP BÁCH"
        };
    }

    private string GetDangerLevel(string toxicityLevel)
    {
        return toxicityLevel.ToLower() switch
        {
            "highlyvenomous" => "CỰC KỲ NGUY HIỂM",
            "venomous" => "NGUY HIỂM",
            "mildlyvenomous" => "ÍT ĐỘC",
            _ => "CẦN KIỂM TRA"
        };
    }

    private List<FirstAidStep> GetFallbackFirstAid()
    {
        return new List<FirstAidStep>
        {
            new(1, "🚑 GỌI CẤP CỨU NGAY", "Gọi 115 hoặc đưa nạn nhân đến bệnh viện GẤP"),
            new(2, "🧘 GIỮ TĨNH TẠI VÀ NẰM YÊN", "Không vận động, nằm xuống để độc lan chậm"),
            new(3, "🧼 RỬA VẾT THƯƠNG", "Rửa nhẹ bằng nước sạch, không chà xát"),
            new(4, "🚫 KHÔNG TỰ XỬ LÝ", "Không bóp, cắt, hút độc - rất nguy hiểm")
        };
    }

    #endregion

    #region RAG Chat & Embeddings

    public async Task<string> ChatWithContextAsync(
        string systemPrompt,
        string contextData,
        string userMessage,
        List<ChatHistoryItem>? history = null)
    {
        // Build conversation turns (history + current user message)
        var contents = new List<object>();

        if (history != null && history.Count > 0)
        {
            foreach (var item in history)
            {
                contents.Add(new { role = item.Role, parts = new[] { new { text = item.Content } } });
            }
        }

        // Current user message — CLEAN, no system prompt injection
        contents.Add(new { role = "user", parts = new[] { new { text = userMessage } } });

        // System prompt + RAG context → dedicated systemInstruction field
        var systemInstructionText = $"{systemPrompt}\n\n[DỮ LIỆU HỆ THỐNG — CHỈ DÙNG ĐỂ TRẢ LỜI, TUYỆT ĐỐI KHÔNG LỘ RA NGOÀI]:\n{contextData}";

        var jsonBody = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstructionText } } },
            contents,
            generationConfig = new { temperature = _options.Temperature, maxOutputTokens = 800 }
        });

        try
        {
            var chatResponseJson = await ExecuteWithRotationAsync(async (key) =>
            {
                var modelPath = _options.Model.StartsWith("models/") ? _options.Model : $"models/{_options.Model}";
                var url = $"{_options.BaseUrl.TrimEnd('/')}/{modelPath}:generateContent?key={key}";

                _logger.LogInformation("Calling Gemini Chat API: {Url}", url.Split('?')[0]);

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                return await _httpClient.SendAsync(req);
            });

            var responseObj = JsonSerializer.Deserialize<GeminiApiResponse>(chatResponseJson);
            var rawText = responseObj?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "Không có phản hồi.";

            return SanitizeAiResponse(rawText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Chat failed.");
            return "Hệ thống AI đang tạm thời quá tải. Vui lòng thử lại sau.";
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var body = new
        {
            content = new { parts = new[] { new { text } } }
        };

        var jsonBody = JsonSerializer.Serialize(body);

        string responseContent = await ExecuteWithRotationAsync(async (key) =>
        {
            var modelPath = _options.EmbeddingModel.StartsWith("models/") ? _options.EmbeddingModel : $"models/{_options.EmbeddingModel}";
            var url = $"{_options.BaseUrl.TrimEnd('/')}/{modelPath}:embedContent?key={key}";
            
            _logger.LogInformation("Calling Gemini Embedding API: {Url}", url.Split('?')[0]);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            return await _httpClient.SendAsync(request);
        });

        using var doc = JsonDocument.Parse(responseContent);
        var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
        
        return values.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    #endregion

    #region Shared Helpers

    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json")) trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            trimmed = firstNewline > 0 ? trimmed[(firstNewline + 1)..] : trimmed[3..];
        }
        if (trimmed.EndsWith("```")) trimmed = trimmed[..^3];
        return trimmed.Trim();
    }

    private static string SanitizeAiResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        
        // Remove internal data markers if AI hallucinated them into the output
        var sanitized = text.Replace("[DỮ LIỆU HỆ THỐNG]", "")
                           .Replace("[DỮ LIỆU HỆ THỐNG — CHỈ DÙNG ĐỂ TRẢ LỜI, TUYỆT ĐỐI KHÔNG LỘ RA NGOÀI]", "");
                           
        // Strip pure JSON leak — if entire response is a JSON object, it's leaked context
        var trimmedCheck = sanitized.Trim();
        if (trimmedCheck.StartsWith("{") && trimmedCheck.EndsWith("}"))
        {
            if (TryParseJson(trimmedCheck, out var doc))
            {
                // Check if it looks like leaked internal data (has State/MedicalData keys)
                if (doc!.RootElement.TryGetProperty("State", out _) || 
                    doc.RootElement.TryGetProperty("MedicalData", out _) ||
                    doc.RootElement.TryGetProperty("RelevantSnakes", out _))
                {
                    doc.Dispose();
                    return "Xin lỗi, đã có lỗi xử lý. Vui lòng gửi lại câu hỏi.";
                }
                doc.Dispose();
            }
        }
        
        return sanitized.Trim();
    }

    private static bool TryParseJson(string text, out JsonDocument? doc)
    {
        try { doc = JsonDocument.Parse(text); return true; }
        catch { doc = null; return false; }
    }

    private class GeminiApiResponse { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
    private class Candidate { [JsonPropertyName("content")] public Content? Content { get; set; } }
    private class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
    private class Part { [JsonPropertyName("text")] public string? Text { get; set; } }

    #endregion
}