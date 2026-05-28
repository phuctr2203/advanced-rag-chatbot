using NTextCat;

namespace PolicyBot.Api.Services.Query;

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
    private readonly RankedLanguageIdentifier? _identifier;

    public LanguageDetectionService(ILogger<LanguageDetectionService> logger)
    {
        _logger = logger;
        _identifier = LoadIdentifier();
    }

    public string Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "en";
        }

        var keywordLanguage = DetectByKeywords(message);
        if (keywordLanguage is not null)
        {
            return keywordLanguage;
        }

        if (_identifier is null)
        {
            return "en";
        }

        try
        {
            var detected = _identifier
                .Identify(message)
                .Select(candidate => candidate.Item1.Iso639_2T)
                .Select(code => Iso6392ToIso6391.GetValueOrDefault(code, string.Empty))
                .FirstOrDefault(code => SupportedLanguages.Contains(code));

            return string.IsNullOrWhiteSpace(detected) ? "en" : detected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Language detection failed. Defaulting to English.");
            return "en";
        }
    }

    private static string? DetectByKeywords(string message)
    {
        var normalized = message.ToLowerInvariant();
        var trimmed = normalized.Trim();

        if (IsAny(trimmed, ["hi", "hello", "hey", "thanks", "thank you", "bye", "goodbye", "ok", "okay", "sure", "great"]))
        {
            return "en";
        }

        if (IsAny(trimmed, ["chào", "xin chào", "cảm ơn", "tạm biệt", "ổn", "được", "vâng", "dạ"]))
        {
            return "vi";
        }

        if (IsAny(trimmed, ["bonjour", "salut", "merci", "au revoir", "bonsoir", "d'accord"]))
        {
            return "fr";
        }

        if (IsAny(trimmed, ["hallo", "guten tag", "danke", "tschüss", "auf wiedersehen", "gut"]))
        {
            return "de";
        }

        if (ContainsAny(normalized, ["chính sách", "nghỉ phép", "lương", "phúc lợi", "quy trình", "tăng ca", "biểu mẫu", "xin chào", "cảm ơn"]))
        {
            return "vi";
        }

        if (ContainsAny(normalized, ["politique", "congé", "salaire", "avantage", "procédure", "formulaire", "bonjour", "merci"]))
        {
            return "fr";
        }

        if (ContainsAny(normalized, ["richtlinie", "urlaub", "gehalt", "verfahren", "überstunden", "formular", "guten tag", "danke"]))
        {
            return "de";
        }

        return null;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAny(string value, IReadOnlyList<string> terms)
    {
        return terms.Any(term => string.Equals(value, term, StringComparison.OrdinalIgnoreCase));
    }

    private RankedLanguageIdentifier? LoadIdentifier()
    {
        var profilePath = FindProfilePath();
        if (profilePath is null)
        {
            _logger.LogWarning("NTextCat Core14.profile.xml was not found. Language detection will default to English unless keyword hints match.");
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
}
