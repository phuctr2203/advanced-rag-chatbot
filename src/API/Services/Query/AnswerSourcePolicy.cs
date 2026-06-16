using System.Globalization;
using System.Text;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public static class AnswerSourcePolicy
{
    private static readonly string[] NoInformationPhrases =
    [
        "no information",
        "no relevant information",
        "couldn't find",
        "could not find",
        "cannot find",
        "can't find",
        "unable to find",
        "not found",
        "does not contain",
        "doesn't contain",
        "do not contain",
        "don't contain",
        "not mention",
        "not provided",
        "not available",
        "no details",
        "khong co thong tin",
        "khong tim thay",
        "khong chua",
        "je n'ai pas trouve",
        "aucune information",
        "ne contient pas",
        "nicht gefunden",
        "keine information",
        "konnte keine",
        "enthalt keine"
    ];

    private static readonly string[] ScopePhrases =
    [
        "provided context",
        "supplied context",
        "given context",
        "context",
        "company documents",
        "indexed documents",
        "documents",
        "tai lieu",
        "contexte",
        "dokumente",
        "unterlagen",
        "kontext"
    ];

    public static bool ShouldHideSources(string answer)
    {
        var normalized = Normalize(answer);
        return ContainsAny(normalized, NoInformationPhrases)
            && ContainsAny(normalized, ScopePhrases);
    }

    public static IReadOnlyList<SourceRef> RemoveSourcesWhenUnsupported(
        string answer,
        IReadOnlyList<SourceRef> sources)
    {
        return ShouldHideSources(answer) ? [] : sources;
    }

    private static bool ContainsAny(string value, IEnumerable<string> phrases)
    {
        return phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
