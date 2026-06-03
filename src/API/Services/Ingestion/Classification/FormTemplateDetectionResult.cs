namespace PolicyBot.Api.Services.Ingestion.Classification;

public class FormTemplateDetectionResult
{
    public bool IsTemplate { get; set; }
    public string Reason { get; set; } = string.Empty;
}
