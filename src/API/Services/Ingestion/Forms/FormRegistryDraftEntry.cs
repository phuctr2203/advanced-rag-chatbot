using System.Text.Json.Serialization;
using PolicyBot.Api.Services.Ingestion.Classification;

namespace PolicyBot.Api.Services.Ingestion.Forms;

public class FormRegistryDraftEntry
{
    [JsonPropertyName("form_name")]
    public string FormName { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("docx_file")]
    public string? DocxFile { get; set; }

    [JsonPropertyName("download_path")]
    public string? DownloadPath { get; set; }

    [JsonPropertyName("agent")]
    public string Agent { get; set; } = DocumentClassifierService.DefaultAgent;
}
