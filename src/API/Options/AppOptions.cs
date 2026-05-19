namespace PolicyBot.Api.Options;

public class LlmProviderOptions
{
    public string Active { get; set; } = "OpenWebUI";
    public LlmEndpointOptions OpenWebUI { get; set; } = new();
    public LlmEndpointOptions Ollama { get; set; } = new();
}

public class LlmEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class EmbeddingOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public int BatchSize { get; set; } = 32;
}

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public int GrpcPort { get; set; } = 6334;
    public string CollectionName { get; set; } = "policy_docs";
}

public class IngestionOptions
{
    public string ImageStorePath { get; set; } = "../../data/images";
    public string TempPath { get; set; } = "../../data/temp";
    public string ChunkingStrategy { get; set; } = "ParagraphBoundary";
    public int ChunkSizeWords { get; set; } = 400;
    public int ChunkOverlapWords { get; set; } = 80;
    public int MinimumChunkWords { get; set; } = 30;
}
