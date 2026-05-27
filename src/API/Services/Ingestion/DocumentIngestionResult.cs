namespace PolicyBot.Api.Services.Ingestion;

public class DocumentIngestionResult
{
    public string FileName { get; set; } = string.Empty;
    public string Agent { get; set; } = DocumentClassifierService.DefaultAgent;
    public int ChunkCount { get; set; }
}
