namespace PolicyBot.Api.Models;

public class ParsedChunk
{
    public string Text { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int Page { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkType { get; set; } = "text";
    public string FileType { get; set; } = string.Empty;
    public string Agent { get; set; } = "ELCA_GENERAL";
    public string ImagePath { get; set; } = string.Empty;
}
