using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public class HybridSearchService(
    IVectorStoreService vectorStoreService,
    KeywordSearchService keywordSearchService,
    IOptions<HybridSearchOptions> options)
{
    private readonly HybridSearchOptions _options = options.Value;

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(string query, CancellationToken ct = default)
    {
        var limit = Math.Max(_options.Limit, 1);
        var candidateLimit = Math.Max(_options.CandidateLimit, limit);
        return await SearchHybridAsync(query, limit, candidateLimit, ct);
    }

    public async Task<HybridSearchDiagnostics> CompareAsync(string query, CancellationToken ct = default)
    {
        var limit = Math.Max(_options.Limit, 1);
        var candidateLimit = Math.Max(_options.CandidateLimit, limit);
        var dense = await vectorStoreService.SearchAsync(query, candidateLimit, ct);
        var keyword = await keywordSearchService.SearchAsync(query, candidateLimit, ct);
        var hybrid = Merge(query, dense, keyword, limit);

        return new HybridSearchDiagnostics
        {
            Query = query,
            Dense = dense.Take(limit).ToList(),
            Keyword = keyword.Take(limit).ToList(),
            Hybrid = hybrid
        };
    }

    private async Task<IReadOnlyList<ScoredChunk>> SearchHybridAsync(
        string query,
        int limit,
        int candidateLimit,
        CancellationToken ct)
    {
        var dense = await vectorStoreService.SearchAsync(query, candidateLimit, ct);
        var keyword = await keywordSearchService.SearchAsync(query, candidateLimit, ct);
        return Merge(query, dense, keyword, limit);
    }

    private IReadOnlyList<ScoredChunk> Merge(
        string query,
        IReadOnlyList<ScoredChunk> dense,
        IReadOnlyList<ScoredChunk> keyword,
        int limit)
    {
        var denseByKey = ToBestResultByKey(dense);
        var keywordByKey = ToBestResultByKey(keyword);
        var maxDense = Math.Max(dense.Count == 0 ? 0 : dense.Max(result => result.Score), 0.0001f);
        var maxKeyword = Math.Max(keyword.Count == 0 ? 0 : keyword.Max(result => result.Score), 0.0001f);
        var useExactWeights = keywordSearchService.HasExactMatchSignals(query);
        var keywordWeight = useExactWeights ? _options.ExactMatchKeywordWeight : _options.KeywordWeight;
        var denseWeight = 1f - keywordWeight;

        return denseByKey.Keys
            .Concat(keywordByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                denseByKey.TryGetValue(key, out var denseResult);
                keywordByKey.TryGetValue(key, out var keywordResult);
                var chunk = denseResult?.Chunk ?? keywordResult!.Chunk;
                var denseScore = denseResult?.Score / maxDense ?? 0;
                var keywordScore = keywordResult?.Score / maxKeyword ?? 0;

                return new ScoredChunk
                {
                    Chunk = chunk,
                    Score = (denseWeight * denseScore) + (keywordWeight * keywordScore)
                };
            })
            .Where(result => result.Score >= _options.MinimumScore)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Chunk.PageNumber)
            .Take(limit)
            .ToList();
    }

    private static string GetKey(ParsedChunk chunk)
    {
        return $"{chunk.SourceFile}|{chunk.PageNumber}|{chunk.ChunkIndex}|{chunk.ChunkType}";
    }

    private static Dictionary<string, ScoredChunk> ToBestResultByKey(IReadOnlyList<ScoredChunk> results)
    {
        return results
            .GroupBy(result => GetKey(result.Chunk), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(result => result.Score).First(),
                StringComparer.OrdinalIgnoreCase);
    }

}

public class HybridSearchDiagnostics
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<ScoredChunk> Dense { get; set; } = [];
    public IReadOnlyList<ScoredChunk> Keyword { get; set; } = [];
    public IReadOnlyList<ScoredChunk> Hybrid { get; set; } = [];
}
