namespace PolicyBot.Api.Models;

public class SourceRef
{
    public string File { get; set; } = string.Empty;
    public int Page { get; set; }
    public string ChunkType { get; set; } = "text";
    public string ImagePath { get; set; } = string.Empty;
    public FormDownloadRef? FormDownload { get; set; }
}

public class FormDownloadRef
{
    public string FormName { get; set; } = string.Empty;
    public string DownloadPath { get; set; } = string.Empty;
}
