using System.Text.Json.Serialization;
using PolicyBot.Api.Services.Ingestion.Classification;

namespace PolicyBot.Api.Services.Ingestion.Forms;

public class FormRegistrySuggestion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("form_name")]
    public string FormName { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("candidate_template_file")]
    public string CandidateTemplateFile { get; set; } = string.Empty;

    [JsonPropertyName("candidate_download_path")]
    public string CandidateDownloadPath { get; set; } = string.Empty;

    [JsonPropertyName("agent")]
    public string Agent { get; set; } = DocumentClassifierService.DefaultAgent;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = FormRegistrySuggestionStatuses.PendingReview;
}

public static class FormRegistrySuggestionStatuses
{
    public const string PendingReview = "pending_review";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string NeedsManualReview = "needs_manual_review";
}
