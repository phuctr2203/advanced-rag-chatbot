namespace PolicyBot.Api.Models;

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
