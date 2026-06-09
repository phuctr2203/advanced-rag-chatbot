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

public class ProviderStatusResponse
{
    public ServiceStatus Llm { get; set; } = new();
    public ServiceStatus Embedding { get; set; } = new();
    public ServiceStatus Qdrant { get; set; } = new();
}

public class ServiceStatus
{
    public string Name { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public string Message { get; set; } = string.Empty;
}
