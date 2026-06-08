using NTextCat;
using PolicyBot.Api.Providers;

namespace PolicyBot.Api.Services.Query;

public class LanguageDetectionResult
{
    public string Language { get; set; } = "en";
    public string Method { get; set; } = "default";
    public float Confidence { get; set; }
}

public class LanguageDetectionService(ILlmProvider llmProvider, ILogger<LanguageDetectionService> logger)
{
    private static readonly HashSet<string> SupportedLanguages = ["en", "vi", "fr", "de"];

    public async Task<LanguageDetectionResult> DetectAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Result("en", "default", 0.1f);
        }

        var deterministic = DetectDeterministic(message);
        if (deterministic is not null)
        {
            return deterministic;
        }

        var llmResult = await DetectWithLlmAsync(message, ct);
        if (llmResult is not null)
        {
            return llmResult;
        }

        return Result("en", "default", 0.1f);
    }

    private static LanguageDetectionResult? DetectDeterministic(string message)
    {
        var normalized = message.ToLowerInvariant();
        var trimmed = normalized.Trim();

        if (IsAny(trimmed, ["hi", "hello", "hey", "thanks", "thank you", "bye", "goodbye", "ok", "okay", "sure", "great"]))
        {
            return Result("en", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["chao", "xin chao", "chào", "xin chào", "cam on", "cảm ơn", "tam biet", "tạm biệt", "duoc", "được", "vang", "vâng", "da", "dạ"]))
        {
            return Result("vi", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["bonjour", "salut", "merci", "au revoir", "bonsoir", "d'accord"]))
        {
            return Result("fr", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["hallo", "guten tag", "danke", "tschuss", "tschüss", "auf wiedersehen", "gut"]))
        {
            return Result("de", "smalltalk", 0.95f);
        }

        if (LooksVietnamese(normalized))
        {
            return Result("vi", "unicode", 0.92f);
        }

        if (ContainsAny(normalized, [
            "chinh sach", "chính sách", "nghi phep", "nghỉ phép", "luong", "lương",
            "phuc loi", "phúc lợi", "quy trinh", "quy trình", "tang ca", "tăng ca",
            "bieu mau", "biểu mẫu", "toa nha", "tòa nhà", "toà nhà", "lam viec", "làm việc"
        ]))
        {
            return Result("vi", "keyword", 0.9f);
        }

        if (ContainsAny(normalized, ["politique", "conge", "congé", "salaire", "avantage", "procedure", "procédure", "formulaire"]))
        {
            return Result("fr", "keyword", 0.9f);
        }

        if (ContainsAny(normalized, ["richtlinie", "urlaub", "gehalt", "verfahren", "uberstunden", "überstunden", "formular"]))
        {
            return Result("de", "keyword", 0.9f);
        }

        return null;
    }

    private async Task<LanguageDetectionResult?> DetectWithLlmAsync(string message, CancellationToken ct)
    {
        var prompt = $"""
            Detect the language of the user message.
            Supported languages:
            - en: English
            - vi: Vietnamese
            - fr: French
            - de: German

            Respond with ONLY one code: en, vi, fr, or de.

            If none of the supported languages can be confidently detected, respond with "na".

            User message: {message}
            """;

        try
        {
            var response = await llmProvider.CompleteAsync(prompt, maxTokens: 5, ct);
            var language = response.Trim().Trim('`', '"', '\'', '.', ',', ':', ';').ToLowerInvariant();
            return SupportedLanguages.Contains(language)
                ? Result(language, "llm", 0.82f)
                : Result("en", "default", 0.1f);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "LLM language detection fallback failed.");
            return null;
        }
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAny(string value, IReadOnlyList<string> terms)
    {
        return terms.Any(term => string.Equals(value, term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksVietnamese(string value)
    {
        return value.Any(character =>
            character is 'ă' or 'â' or 'đ' or 'ê' or 'ô' or 'ơ' or 'ư'
            || character is >= '\u1ea0' and <= '\u1ef9');
    }

    private static LanguageDetectionResult Result(string language, string method, float confidence)
    {
        return new LanguageDetectionResult
        {
            Language = language,
            Method = method,
            Confidence = confidence
        };
    }
}
