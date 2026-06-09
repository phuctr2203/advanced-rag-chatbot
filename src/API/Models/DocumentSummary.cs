namespace PolicyBot.Api.Models;

public class DocumentSummary
{
    public string SourceFile { get; set; } = string.Empty;
    public string DownloadPath { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int PageCount { get; set; }
    public bool HasImages { get; set; }
    public bool HasFormTemplate { get; set; }
}
