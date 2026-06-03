namespace PolicyBot.Api.Options;

public class LlmProviderOptions
{
    public string Active { get; set; } = "Ollama";
    public LlmEndpointOptions OpenWebUI { get; set; } = new();
    public LlmEndpointOptions Ollama { get; set; } = new();
}

public class LlmEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class VisionProviderOptions
{
    public string Active { get; set; } = "Ollama";
    public LlmEndpointOptions OpenWebUI { get; set; } = new();
    public LlmEndpointOptions Ollama { get; set; } = new();
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
    public string TemplatesStorePath { get; set; } = "../../data/templates";
    public string UploadedDocumentsPath { get; set; } = "../../data/uploads";
    public string DataPath { get; set; } = "../../data";
    public string TempPath { get; set; } = "../../data/temp";
    public string ChunkingStrategy { get; set; } = "ParagraphBoundary";
    public int ChunkSizeWords { get; set; } = 400;
    public int ChunkOverlapWords { get; set; } = 80;
    public int MinimumChunkWords { get; set; } = 30;
    public bool EnableImageCaptioning { get; set; } = true;
    public int MaxImageCaptionsPerDocument { get; set; }
    public bool EnablePdfOcrFallback { get; set; } = true;
    public int PdfOcrMinimumTextCharacters { get; set; } = 100;
    public int PdfOcrMinimumAverageWordsPerPage { get; set; } = 10;
    public string OcrMyPdfExecutable { get; set; } = "ocrmypdf";
}
