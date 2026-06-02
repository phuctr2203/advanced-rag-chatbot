namespace PolicyBot.Api.Models;

public class ScoredChunk
{
    public ParsedChunk Chunk { get; set; } = new();
    public float Score { get; set; }
}
