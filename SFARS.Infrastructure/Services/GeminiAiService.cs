using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFARS.Domain.Interfaces.Infrastructure;
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
                
                var jsonText = resultText.Trim();
                if (jsonText.StartsWith("```json"))
                {
                    jsonText = jsonText.Substring(7);
                    if (jsonText.EndsWith("```"))
                    {
                        jsonText = jsonText.Substring(0, jsonText.Length - 3);
                    }
                    jsonText = jsonText.Trim();
                    _logger.LogInformation("Stripped markdown wrapper from Gemini response");
                }
                else if (jsonText.StartsWith("```"))
                {
                    var firstNewline = jsonText.IndexOf('\n');
                    if (firstNewline > 0)
                    {
                        jsonText = jsonText.Substring(firstNewline + 1);
                    }
                    if (jsonText.EndsWith("```"))
                    {
                        jsonText = jsonText.Substring(0, jsonText.Length - 3);
                    }
                    jsonText = jsonText.Trim();
                    _logger.LogInformation("Stripped generic markdown wrapper from Gemini response");
                }
                
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

        int maxRetries = 3;
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

                // var result = JsonSerializer.Deserialize<GeminiApiResponse>(json);

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

    // Gemini API response models
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
}