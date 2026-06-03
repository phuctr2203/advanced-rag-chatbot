namespace PolicyBot.Api.Services.Ingestion.Images;

public class PdfImageAnalysisResult
{
    public int Page { get; set; }
    public int ImageIndex { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float Ratio { get; set; }
    public bool HasImageBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public int ByteCount { get; set; }
    public bool PassesSizeFilter { get; set; }
    public bool PassesAspectRatioFilter { get; set; }
    public bool IsFullPageImage { get; set; }
}
