namespace PolicyBot.Api.Services.Ingestion.Storage;

public class StoredDocument
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string UrlPath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public bool AlreadyExisted { get; set; }
}
