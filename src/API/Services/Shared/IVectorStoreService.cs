using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Shared;

public interface IVectorStoreService
{
    Task UpsertAsync(IReadOnlyList<ParsedChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken ct = default);
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, string? agent, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(string query, int limit = 6, CancellationToken ct = default);
    Task<IReadOnlyList<ParsedChunk>> ListChunksAsync(int limit = 2048, CancellationToken ct = default);
    Task<IReadOnlyList<ParsedChunk>> ListAllChunksAsync(CancellationToken ct = default);
    Task<int> DeleteBySourceFileAsync(string sourceFile, CancellationToken ct = default);
    Task CheckHealthAsync(CancellationToken ct = default);
}
