namespace PolicyBot.Api.Services.Ingestion.Forms;

public class ChooseTemplateRequest
{
    public string CandidateTemplateFile { get; set; } = string.Empty;
    public string CandidateDownloadPath { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
