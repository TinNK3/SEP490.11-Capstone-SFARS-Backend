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

    public string ModelName => _options.Model;

    public GeminiAiService(
        ILogger<GeminiAiService> logger,
        IOptions<GeminiOptions> options,
        HttpClient httpClient)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    #region Snake Detection (Gemini Vision — replaces binary ONNX model)

    private const string SnakeDetectionSystemPrompt =
        """
        Bạn là chuyên gia AI cao cấp cho hệ thống phản ứng cấp cứu y tế.
        Nhiệm vụ: Phân tích hình ảnh và xác định DUY NHẤT một câu hỏi: "Đây có phải là một con RẮN THẬT, CÒN SỐNG (hoặc mới chết) mang tính chất đe dọa sinh học hay không?"
        
        QUY TẮC NGHIÊM NGẶT:
        1. TRẢ VỀ is_snake: true NẾU:
           - Là thực thể sinh học tự nhiên (biological organism).
           - Có bề mặt da vảy tự nhiên, mắt có độ sâu, hoặc tư thế vận động đặc trưng.
        
        2. TRẢ VỀ is_snake: false NẾU LÀ:
           - Đồ chơi nhựa, cao su, gỗ, đá (thường có khớp nối, bề mặt bóng bẩy công nghiệp hoặc tư thế bất tử cứng nhắc).
           - Hình ảnh chụp lại từ màn hình thiết bị khác (có viền màn hình, hiện tượng lóa sáng điểm ảnh).
           - Hình vẽ, phim hoạt hình, trang sức, túi xách/quần áo có họa tiết da rắn.
        
        BẮT BUỘC TRẢ VỀ JSON (không giải thích thêm):
        {"is_snake": true/false, "confidence": float, "reasoning": "Lý do ngắn gọn để AI tự kiểm chứng"}
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
                temperature = 0.1,   // Low temperature for deterministic classification
                maxOutputTokens = 256 // Short response expected
            }
        };

        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        int maxRetries = _options.MaxRetries + 1;
        int delay = 500;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json")
                };

                var response = await _httpClient.SendAsync(requestMsg);

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning(
                            "Gemini 503 (snake detection), retry {Attempt} after {Delay}ms",
                            attempt + 1, delay);
                        await Task.Delay(delay);
                        delay *= 2;
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Gemini snake detection API error {StatusCode}: {Error}",
                        response.StatusCode, errorContent);
                }

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Gemini snake detection raw response: {Response}", responseContent);

                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
                var resultText = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;

                if (string.IsNullOrEmpty(resultText))
                {
                    throw new InvalidOperationException("Gemini returned empty response for snake detection.");
                }

                return ParseSnakeDetectionResponse(resultText);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Gemini snake detection HTTP error, retry {Attempt}", attempt + 1);
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        // Exhausted retries — fallback: assume snake to be safe (avoid missing real emergencies)
        _logger.LogWarning("Gemini snake detection failed after all retries, falling back to IsSnake=true");
        return new GeminiSnakeDetectionResult(IsSnake: true, Confidence: 0.5f, Reasoning: "Không thể kết nối AI xác nhận. Mặc định xác nhận có rắn để đảm bảo an toàn.");
    }

    /// <summary>
    /// Parse Gemini's text response into a structured detection result.
    /// Handles markdown code fences and case-insensitive JSON properties.
    /// </summary>
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

            return new GeminiSnakeDetectionResult(
                IsSnake: parsed.IsSnake,
                Confidence: Math.Clamp(parsed.Confidence, 0f, 1f),
                Reasoning: parsed.Reasoning
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini snake detection response: {Text}", jsonText);
            // Safe fallback: assume snake
            return new GeminiSnakeDetectionResult(IsSnake: true, Confidence: 0.5f, Reasoning: "Không thể phân tích kết quả AI. Mặc định xác nhận có rắn.");
        }
    }

    private sealed class SnakeDetectionJsonResponse
    {
        [JsonPropertyName("is_snake")]
        public bool IsSnake { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }
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

    private sealed class VoiceExtractionJsonResponse
    {
        [JsonPropertyName("transcript")]
        public string? Transcript { get; set; }

        [JsonPropertyName("minutes_since_bite")]
        public int? MinutesSinceBite { get; set; }

        [JsonPropertyName("symptoms")]
        public List<string>? Symptoms { get; set; }
    }

    /// <inheritdoc />
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

        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        int maxRetries = _options.MaxRetries;
        int delay = 500;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json"
                    )
                };

                var response = await _httpClient.SendAsync(requestMsg);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Gemini STT API error {StatusCode}: {Error}", response.StatusCode, error);
                    response.EnsureSuccessStatusCode();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
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
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Gemini STT HTTP error, retry {Attempt}", attempt + 1);
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        _logger.LogWarning("Gemini STT failed after all retries.");
        return null;
    }

    #endregion

    public async Task<GeminiAnalysisResult> AnalyzeSnakeBiteAsync(GeminiAnalysisRequest request)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPayload = BuildUserPayload(request);
        
        var body = new
        {
            systemInstruction = new  // ← camelCase, not snake_case!
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = JsonSerializer.Serialize(userPayload) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = 1024
            }
        };

        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            // Retry logic for 503 (high demand) - matching TS reference code
            int maxRetries = 3;
            int delay = 500; // ms
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                using var request_msg = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json")
                };

                var response = await _httpClient.SendAsync(request_msg);
                
                // Log error response if not successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error {StatusCode}: {Error}", response.StatusCode, errorContent);
                }
                
                // Retry on 503 (Service Unavailable / high demand)
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning("Gemini 503 (high demand), retry lần {Attempt} sau {Delay}ms", attempt + 1, delay);
                        await Task.Delay(delay);
                        delay *= 2; // exponential backoff: 500ms, 1000ms, 2000ms
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning("Gemini 503 after {Max} retries, using fallback", maxRetries);
                    }
                }
                
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Gemini raw response: {Response}", responseContent);
                
                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);

                var resultText = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;
                if (string.IsNullOrEmpty(resultText))
                {
                    _logger.LogWarning("Gemini response structure: Candidates={CandidatesCount}, Content={HasContent}", 
                        geminiResponse?.Candidates?.Count ?? 0,
                        geminiResponse?.Candidates?[0]?.Content != null);
                    throw new InvalidOperationException($"Gemini returned empty response. Full response: {responseContent}");
                }

                _logger.LogInformation("Gemini result text: {ResultText}", resultText);
                
                var jsonText = StripMarkdownCodeFence(resultText);
                
                var result = JsonSerializer.Deserialize<GeminiAnalysisResult>(jsonText);
                if (result == null)
                {
                    throw new InvalidOperationException($"Failed to parse Gemini response: {jsonText}");
                }

                _logger.LogInformation("Gemini analysis completed successfully");
                return result;
            }
            
            throw new InvalidOperationException("Retry logic exhausted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            
            // Return fallback response
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
            new(1, "GỌI CẤP CỨU NGAY", "Gọi 115 hoặc đưa nạn nhân đến bệnh viện GẤP"),
            new(2, "GIỮ TĨNH TẠI VÀ NẰM YÊN", "Không vận động, nằm xuống để độc lan chậm"),
            new(3, "RỬA VẾT THƯƠNG", "Rửa nhẹ bằng nước sạch, không chà xát"),
            new(4, "KHÔNG TỰ XỬ LÝ", "Không bóp, cắt, hút độc - rất nguy hiểm")
        };
    }

    /// <summary>
    /// RAG-based chat with Gemini. Sends system prompt + DB context + conversation history.
    /// </summary>
    public async Task<string> ChatWithContextAsync(
        string systemPrompt,
        string contextData,
        string userMessage,
        List<ChatHistoryItem>? history = null)
    {
        // Build multi-turn contents
        var contents = new List<object>();

        // First message: context + first user message OR just context
        if (history != null && history.Count > 0)
        {
            // Add context as first user turn
            contents.Add(new { role = "user", parts = new[] { new { text = $"[DỮ LIỆU HỆ THỐNG]\n{contextData}" } } });
            contents.Add(new { role = "model", parts = new[] { new { text = "Đã nhận dữ liệu hệ thống. Tôi sẽ chỉ trả lời dựa trên dữ liệu này." } } });

            // Add history
            foreach (var item in history)
            {
                contents.Add(new { role = item.Role, parts = new[] { new { text = item.Content } } });
            }
        }

        // Add current user message (with context if no history)
        if (history == null || history.Count == 0)
        {
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = $"[DỮ LIỆU HỆ THỐNG]\n{contextData}\n\n[CÂU HỎI]\n{userMessage}" } }
            });
        }
        else
        {
            contents.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
        }

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents,
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = 1024
            }
        };

        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        int maxRetries = _options.MaxRetries + 1;
        int delay = 500;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json")
                };

                var response = await _httpClient.SendAsync(requestMsg);

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning("Gemini 503 (chat), retry {Attempt} after {Delay}ms", attempt + 1, delay);
                        await Task.Delay(delay);
                        delay *= 2;
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var result = JsonSerializer.Deserialize<GeminiApiResponse>(json, options);
                var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                return text ?? "Xin lỗi, mình không thể trả lời lúc này. Vui lòng thử lại.";
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Gemini chat HTTP error, retry {Attempt}", attempt + 1);
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        return "Hệ thống AI đang tạm thời quá tải. Vui lòng thử lại sau.";
    }

    #region Shared Helpers

    /// <summary>
    /// Strip markdown code fences (```json ... ``` or ``` ... ```) from Gemini response text.
    /// Gemini occasionally wraps JSON in markdown code blocks.
    /// </summary>
    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed[7..]; // Remove ```json
        }
        else if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            trimmed = firstNewline > 0 ? trimmed[(firstNewline + 1)..] : trimmed[3..];
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    #endregion

    #region Gemini API Response Models

    private class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    #endregion
}