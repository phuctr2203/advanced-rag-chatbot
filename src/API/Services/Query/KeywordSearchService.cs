using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public partial class KeywordSearchService(
    IVectorStoreService vectorStoreService,
    IOptions<HybridSearchOptions> options)
{
    private readonly HybridSearchOptions _options = options.Value;

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string query,
        int? limit = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var candidateLimit = Math.Max(_options.CandidateLimit, limit ?? _options.Limit);
        var chunks = await vectorStoreService.ListChunksAsync(Math.Max(candidateLimit * 20, 512), ct);
        var scored = chunks
            .Select(chunk => new ScoredChunk
            {
                Chunk = chunk,
                Score = Score(query, chunk)
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Chunk.PageNumber)
            .Take(limit ?? _options.Limit)
            .ToList();

        return scored;
    }

    public float Score(string query, ParsedChunk chunk)
    {
        var normalizedQuery = Normalize(query);
        var normalizedText = Normalize(BuildSearchText(chunk));
        if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedText))
        {
            return 0;
        }

        var queryTerms = Tokenize(normalizedQuery);
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var exactTokens = ExtractIdentifierTokens(query);
        var matches = queryTerms.Count(term => normalizedText.Contains(term, StringComparison.Ordinal));
        var coverage = (float)matches / queryTerms.Count;
        var score = coverage * 0.55f;

        if (normalizedText.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 0.3f;
        }

        foreach (var exactToken in exactTokens)
        {
            var normalizedExact = Normalize(exactToken);
            if (normalizedText.Contains(normalizedExact, StringComparison.Ordinal))
            {
                score += 0.25f;
            }
        }

        if (chunk.IsFormTemplate && normalizedQuery.Contains("form", StringComparison.Ordinal))
        {
            score += 0.1f;
        }

        return Math.Min(score, 1f);
    }

    public bool HasExactMatchSignals(string query)
    {
        return ExtractIdentifierTokens(query).Count > 0
            || query.Contains("form", StringComparison.OrdinalIgnoreCase)
            || query.Contains("article", StringComparison.OrdinalIgnoreCase)
            || query.Contains("điều", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSearchText(ParsedChunk chunk)
    {
        return string.Join(' ', [
            chunk.Text,
            chunk.SourceFile,
            chunk.ChunkType,
            chunk.FileType,
            chunk.Agent,
            chunk.TemplatePath,
            chunk.ImagePath
        ]);
    }

    private static List<string> Tokenize(string value)
    {
        return WordRegex()
            .Matches(value)
            .Select(match => match.Value)
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ExtractIdentifierTokens(string query)
    {
        return IdentifierRegex()
            .Matches(query)
            .Select(match => match.Value)
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return SpaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\b(?:[A-Z]{2,}(?:-[A-Z0-9]+)+|[A-Z]{2,}|[0-9]+(?:\.[0-9]+)*|(?i:Article|Điều)\s+[0-9]+)\b", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex SpaceRegex();
}
