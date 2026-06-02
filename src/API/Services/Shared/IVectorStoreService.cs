using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Shared;

public interface IVectorStoreService
{
    Task UpsertAsync(IReadOnlyList<ParsedChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken ct = default);
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, string? agent, int limit, CancellationToken ct = default);
}
