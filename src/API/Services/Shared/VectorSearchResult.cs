using PolicyBot.Api.Models;

namespace PolicyBot.Api.Services.Shared;

public class VectorSearchResult
{
    public ParsedChunk Chunk { get; set; } = new();
    public float Score { get; set; }
}
