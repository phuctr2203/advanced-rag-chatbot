using NTextCat;

namespace PolicyBot.Api.Services.Query;

public class LanguageDetectionResult
{
    public string Language { get; set; } = "en";
    public string Method { get; set; } = "default";
    public float Confidence { get; set; }
}

public class LanguageDetectionService
{
    private static readonly HashSet<string> SupportedLanguages = ["en", "vi", "fr", "de"];
    private static readonly Dictionary<string, string> Iso6392ToIso6391 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "en",
        ["vie"] = "vi",
        ["fra"] = "fr",
        ["fre"] = "fr",
        ["deu"] = "de",
        ["ger"] = "de"
    };

    private readonly ILogger<LanguageDetectionService> _logger;
    private readonly LlmService _llmService;
    private readonly RankedLanguageIdentifier? _identifier;

    public LanguageDetectionService(ILogger<LanguageDetectionService> logger, LlmService llmService)
    {
        _logger = logger;
        _llmService = llmService;
        _identifier = LoadIdentifier();
    }

    public async Task<LanguageDetectionResult> DetectAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new LanguageDetectionResult { Language = "en", Method = "default", Confidence = 0.1f };
        }

        var deterministic = DetectDeterministic(message);
        if (deterministic is not null)
        {
            return deterministic;
        }

        var candidate = DetectWithNTextCat(message);
        if (candidate.Confidence >= 0.75f)
        {
            return candidate;
        }

        var llmResult = await DetectWithLlmAsync(message, ct);
        if (llmResult is not null)
        {
            return llmResult;
        }

        return SupportedLanguages.Contains(candidate.Language)
            ? candidate
            : new LanguageDetectionResult { Language = "en", Method = "default", Confidence = 0.1f };
    }

    public string Detect(string message)
    {
        return DetectAsync(message).GetAwaiter().GetResult().Language;
    }

    private static LanguageDetectionResult? DetectDeterministic(string message)
    {
        var normalized = message.ToLowerInvariant();
        var trimmed = normalized.Trim();

        if (IsAny(trimmed, ["hi", "hello", "hey", "thanks", "thank you", "bye", "goodbye", "ok", "okay", "sure", "great"]))
        {
            return Result("en", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["ch\u00e0o", "xin ch\u00e0o", "c\u1ea3m \u01a1n", "t\u1ea1m bi\u1ec7t", "\u1ed5n", "\u0111\u01b0\u1ee3c", "v\u00e2ng", "d\u1ea1"]))
        {
            return Result("vi", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["bonjour", "salut", "merci", "au revoir", "bonsoir", "d'accord"]))
        {
            return Result("fr", "smalltalk", 0.95f);
        }

        if (IsAny(trimmed, ["hallo", "guten tag", "danke", "tsch\u00fcss", "auf wiedersehen", "gut"]))
        {
            return Result("de", "smalltalk", 0.95f);
        }

        if (LooksVietnamese(normalized))
        {
            return Result("vi", "unicode", 0.92f);
        }

        if (ContainsAny(normalized, [
            "ch\u00ednh s\u00e1ch", "ngh\u1ec9 ph\u00e9p", "l\u01b0\u01a1ng", "ph\u00fac l\u1ee3i",
            "quy tr\u00ecnh", "t\u0103ng ca", "bi\u1ec3u m\u1eabu", "xin ch\u00e0o", "c\u1ea3m \u01a1n",
            "t\u00f2a nh\u00e0", "to\u00e0 nh\u00e0", "l\u00e0m vi\u1ec7c", "h\u00e0ng ng\u00e0y",
            "m\u1ea5y gi\u1edd", "th\u1eddi gian"
        ]))
        {
            return Result("vi", "keyword", 0.9f);
        }

        if (ContainsAny(normalized, ["politique", "cong\u00e9", "salaire", "avantage", "proc\u00e9dure", "formulaire", "bonjour", "merci"]))
        {
            return Result("fr", "keyword", 0.9f);
        }

        if (ContainsAny(normalized, ["richtlinie", "urlaub", "gehalt", "verfahren", "\u00fcberstunden", "formular", "guten tag", "danke"]))
        {
            return Result("de", "keyword", 0.9f);
        }

        return null;
    }

    private LanguageDetectionResult DetectWithNTextCat(string message)
    {
        if (_identifier is null)
        {
            return Result("en", "default", 0.1f);
        }

        try
        {
            var detected = _identifier
                .Identify(message)
                .Select(candidate => candidate.Item1.Iso639_2T)
                .Select(code => Iso6392ToIso6391.GetValueOrDefault(code, string.Empty))
                .FirstOrDefault(code => SupportedLanguages.Contains(code));

            if (string.IsNullOrWhiteSpace(detected))
            {
                return Result("en", "default", 0.1f);
            }

            var confidence = message.Trim().Length < 24 ? 0.55f : 0.7f;
            return Result(detected, "ntextcat", confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Language detection failed. Defaulting to English.");
            return Result("en", "default", 0.1f);
        }
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

            User message: {message}
            """;

        try
        {
            var response = await _llmService.CompleteAsync(prompt, maxTokens: 5, ct);
            var language = response.Trim().Trim('`', '"', '\'', '.', ',', ':', ';').ToLowerInvariant();
            return SupportedLanguages.Contains(language)
                ? Result(language, "llm", 0.82f)
                : Result("en", "default", 0.1f);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "LLM language detection fallback failed.");
            return null;
        }
    }

    private RankedLanguageIdentifier? LoadIdentifier()
    {
        var profilePath = FindProfilePath();
        if (profilePath is null)
        {
            _logger.LogWarning("NTextCat Core14.profile.xml was not found. Language detection will use deterministic and LLM fallback only.");
            return null;
        }

        try
        {
            var factory = new RankedLanguageIdentifierFactory();
            return factory.Load(profilePath, model => Iso6392ToIso6391.ContainsKey(model.Language.Iso639_2T));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load NTextCat language profile from {ProfilePath}.", profilePath);
            return null;
        }
    }

    private static string? FindProfilePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Core14.profile.xml"),
            Path.Combine(Directory.GetCurrentDirectory(), "Core14.profile.xml"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages",
                "ntextcat",
                "0.3.65",
                "content",
                "Core14.profile.xml")
        };

        return candidates.FirstOrDefault(File.Exists);
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
            character is '\u0103' or '\u00e2' or '\u0111' or '\u00ea' or '\u00f4' or '\u01a1' or '\u01b0'
            || character is '\u1ea0' or '\u1ea1' or '\u1ea2' or '\u1ea3' or '\u1ea4' or '\u1ea5'
            || character is '\u1ea6' or '\u1ea7' or '\u1ea8' or '\u1ea9' or '\u1eaa' or '\u1eab'
            || character is '\u1eac' or '\u1ead' or '\u1eae' or '\u1eaf' or '\u1eb0' or '\u1eb1'
            || character is '\u1eb2' or '\u1eb3' or '\u1eb4' or '\u1eb5' or '\u1eb6' or '\u1eb7'
            || character is '\u1eb8' or '\u1eb9' or '\u1eba' or '\u1ebb' or '\u1ebc' or '\u1ebd'
            || character is '\u1ebe' or '\u1ebf' or '\u1ec0' or '\u1ec1' or '\u1ec2' or '\u1ec3'
            || character is '\u1ec4' or '\u1ec5' or '\u1ec6' or '\u1ec7' or '\u1ec8' or '\u1ec9'
            || character is '\u1eca' or '\u1ecb' or '\u1ecc' or '\u1ecd' or '\u1ece' or '\u1ecf'
            || character is '\u1ed0' or '\u1ed1' or '\u1ed2' or '\u1ed3' or '\u1ed4' or '\u1ed5'
            || character is '\u1ed6' or '\u1ed7' or '\u1ed8' or '\u1ed9' or '\u1eda' or '\u1edb'
            || character is '\u1edc' or '\u1edd' or '\u1ede' or '\u1edf' or '\u1ee0' or '\u1ee1'
            || character is '\u1ee2' or '\u1ee3' or '\u1ee4' or '\u1ee5' or '\u1ee6' or '\u1ee7'
            || character is '\u1ee8' or '\u1ee9' or '\u1eea' or '\u1eeb' or '\u1eec' or '\u1eed'
            || character is '\u1eee' or '\u1eef' or '\u1ef0' or '\u1ef1' or '\u1ef2' or '\u1ef3'
            || character is '\u1ef4' or '\u1ef5' or '\u1ef6' or '\u1ef7' or '\u1ef8' or '\u1ef9');
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
