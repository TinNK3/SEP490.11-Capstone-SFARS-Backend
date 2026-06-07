using System.Text.RegularExpressions;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Application.Common;

/// <summary>
/// Post-check guard for AI chatbox responses.
/// Scans for banned content: drug dosages, surgical instructions, dangerous interventions.
/// </summary>
public static class ChatGuard
{
    /// <summary>
    /// Hard-bans: things that are always dangerous to suggest (dosages, surgery, tourniquet, self-inject).
    /// </summary>
    private static readonly Regex[] HardBannedPatterns = new[]
    {
        // Surgical/invasive instructions
        new Regex(@"\b(rạch|cắt|mổ|trích|khâu|phẫu\s*thuật|tiểu\s*phẫu)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Self-injection/infusion
        new Regex(@"\b(tự\s*tiêm|tự\s*truyền|truyền\s*dịch|tiêm\s*tĩnh\s*mạch|tiêm\s*bắp)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Tourniquet/garo instructions
        new Regex(@"\b(garo|garô|buộc\s*chặt\s*chi|thắt\s*garô)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        
        // Specific dosages format: number + unit
        new Regex(@"\b\d+\s*(mg|ml|viên|ống|liều|cc)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    /// <summary>
    /// Contextual bans: drug names that are ONLY banned if accompanied by verbs implying self-administration.
    /// E.g., "uống paracetamol", "tiêm huyết thanh".
    /// Not banned if just mentioned like "đến bệnh viện có huyết thanh".
    /// </summary>
    private static readonly Regex[] ContextualBannedPatterns = new[]
    {
        // Common pain/fever/anti-inflammatory meds + antivenom IF preceded by action verbs
        new Regex(@"\b(uống|dùng|tiêm|chích|truyền|tự\s*tiêm)\b.{0,30}\b(paracetamol|ibuprofen|aspirin|morphin|adrenalin|epinephrin|huyết\s*thanh(\s*kháng\s*nọc)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>
    /// Validate AI response. Returns safe=true if no banned content found.
    /// Handles false positives (e.g., "Không được cắt") by checking preceding words.
    /// Provides an auto-sanitized message for contextual violations instead of a hard block.
    /// </summary>
    public static (bool IsSafe, string? Violation, string SanitizedResponse) Validate(string aiResponse)
    {
        var lowerResponse = aiResponse.ToLowerInvariant();
        var negations = new[] { "không", "đừng", "chớ", "tránh", "cấm", "tuyệt đối không" };

        // 1. Check Hard Bans
        foreach (var pattern in HardBannedPatterns)
        {
            var matches = pattern.Matches(aiResponse);
            foreach (Match match in matches)
            {
                if (IsNegated(lowerResponse, match.Index, negations))
                {
                    continue; // False positive
                }
                return (false, $"Hard-ban detected: '{match.Value}'", string.Empty);
            }
        }

        // 2. Check Contextual Bans (Drugs + Administration)
        string sanitizedOutput = aiResponse;
        bool hasContextualViolation = false;
        string? firstViolation = null;

        foreach (var pattern in ContextualBannedPatterns)
        {
            var matches = pattern.Matches(sanitizedOutput);
            for (int i = matches.Count - 1; i >= 0; i--) // Go backward to avoid index shift on replace
            {
                Match match = matches[i];
                if (IsNegated(lowerResponse, match.Index, negations))
                {
                    continue; // False positive
                }

                hasContextualViolation = true;
                firstViolation ??= match.Value;

                // Auto-sanitize: Replace the offending sentence/phrase with a safe warning
                // Find start and end of sentence
                int sentenceStart = sanitizedOutput.LastIndexOf('.', match.Index) + 1;
                if (sentenceStart == 0) sentenceStart = sanitizedOutput.LastIndexOf('\n', match.Index) + 1;
                
                int sentenceEnd = sanitizedOutput.IndexOf('.', match.Index + match.Length);
                if (sentenceEnd == -1) sentenceEnd = sanitizedOutput.IndexOf('\n', match.Index + match.Length);
                if (sentenceEnd == -1) sentenceEnd = sanitizedOutput.Length;
                else sentenceEnd += 1; // Include the period

                string before = sanitizedOutput.Substring(0, sentenceStart);
                string after = sanitizedOutput.Substring(sentenceEnd);
                
                sanitizedOutput = before + " [CẢNH BÁO: Tuyệt đối không tự ý dùng thuốc/huyết thanh tại nhà. Phải đến cơ sở y tế ngay lập tức.] " + after;
            }
        }

        if (hasContextualViolation)
        {
            return (false, $"Contextual-ban detected: '{firstViolation}'", sanitizedOutput.Replace("  ", " ").Trim());
        }

        return (true, null, aiResponse);
    }

    private static bool IsNegated(string lowerResponse, int matchIndex, string[] negations)
    {
        int start = Math.Max(0, matchIndex - 30);
        string prefix = lowerResponse.Substring(start, matchIndex - start);

        foreach (var neg in negations)
        {
            if (Regex.IsMatch(prefix, $@"\b{neg}\b"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get safe fallback message from system messages.
    /// </summary>
    public static async Task<string> GetSafeFallbackAsync(ISystemMessageService msgService)
    {
        return await msgService.GetMessageAsync(ResultCodeConst.Chat_Guard0001);
    }
}