namespace PolicyBot.Api.Models;

public class DocumentIndexState
{
    public string SourceFile { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string IngestedAt { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
}
