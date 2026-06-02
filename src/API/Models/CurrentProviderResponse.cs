namespace PolicyBot.Api.Models;

public class CurrentProviderResponse
{
    public string LlmProvider { get; set; } = string.Empty;
    public string LlmModel { get; set; } = string.Empty;
    public string VisionProvider { get; set; } = string.Empty;
    public string VisionModel { get; set; } = string.Empty;
    public string EmbeddingProvider { get; set; } = "TEI";
    public string EmbeddingBaseUrl { get; set; } = string.Empty;
}
