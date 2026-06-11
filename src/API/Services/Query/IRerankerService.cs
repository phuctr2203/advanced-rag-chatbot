using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Query;

public interface IRerankerService
{
    Task<RerankResponse> RerankAsync(
        string query,
        IReadOnlyList<ScoredChunk> candidates,
        int limit,
        CancellationToken ct = default);
}

public class RerankResponse
{
    public bool Used { get; set; }
    public string? FallbackReason { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public IReadOnlyList<ScoredChunk> Results { get; set; } = [];
}
