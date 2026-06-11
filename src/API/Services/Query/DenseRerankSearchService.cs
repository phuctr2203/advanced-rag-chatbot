using System.Diagnostics;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Query;

public class DenseRerankSearchService(
    IVectorStoreService vectorStoreService,
    IRerankerService rerankerService,
    IOptions<RetrievalOptions> options,
    IOptions<RerankerOptions> rerankerOptions)
{
    private readonly RetrievalOptions _options = options.Value;
    private readonly RerankerOptions _rerankerOptions = rerankerOptions.Value;

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        var diagnostics = await SearchWithDiagnosticsAsync(query, limit: null, ct);
        return diagnostics.Results;
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string query,
        int? limit,
        CancellationToken ct = default)
    {
        var diagnostics = await SearchWithDiagnosticsAsync(query, limit, ct);
        return diagnostics.Results;
    }

    public async Task<DenseRerankSearchDiagnostics> SearchWithDiagnosticsAsync(
        string query,
        int? limit,
        CancellationToken ct = default)
    {
        var finalLimit = Math.Max(limit ?? _options.FinalLimit, 1);
        var candidateLimit = Math.Max(_options.CandidateLimit, finalLimit);

        var denseStopwatch = Stopwatch.StartNew();
        var denseCandidates = await vectorStoreService.SearchAsync(query, candidateLimit, ct);
        denseStopwatch.Stop();

        if (denseCandidates.Count == 0)
        {
            return new DenseRerankSearchDiagnostics
            {
                Query = query,
                DenseCandidates = [],
                Results = [],
                DenseRetrievalMilliseconds = denseStopwatch.ElapsedMilliseconds,
                RerankingMilliseconds = 0,
                RerankerEnabled = false,
                RerankerUsed = false,
                FallbackReason = "no_dense_candidates"
            };
        }

        var rerankResponse = await rerankerService.RerankAsync(query, denseCandidates, finalLimit, ct);

        return new DenseRerankSearchDiagnostics
        {
            Query = query,
            DenseCandidates = denseCandidates,
            Results = rerankResponse.Results,
            DenseRetrievalMilliseconds = denseStopwatch.ElapsedMilliseconds,
            RerankingMilliseconds = rerankResponse.ElapsedMilliseconds,
            RerankerEnabled = _rerankerOptions.Enabled,
            RerankerUsed = rerankResponse.Used,
            FallbackReason = rerankResponse.FallbackReason
        };
    }
}

public class DenseRerankSearchDiagnostics
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<ScoredChunk> DenseCandidates { get; set; } = [];
    public IReadOnlyList<ScoredChunk> Results { get; set; } = [];
    public long DenseRetrievalMilliseconds { get; set; }
    public long RerankingMilliseconds { get; set; }
    public bool RerankerEnabled { get; set; }
    public bool RerankerUsed { get; set; }
    public string? FallbackReason { get; set; }
}
