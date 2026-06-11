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

public class HybridSearchOptions
{
    public float DenseWeight { get; set; } = 0.7f;
    public float KeywordWeight { get; set; } = 0.3f;
    public float ExactMatchKeywordWeight { get; set; } = 0.5f;
    public int Limit { get; set; } = 6;
    public int CandidateLimit { get; set; } = 20;
    public float MinimumScore { get; set; } = 0.45f;
}

public class RetrievalOptions
{
    public string Mode { get; set; } = "DenseRerank";
    public int FinalLimit { get; set; } = 4;
    public int CandidateLimit { get; set; } = 10;
    public float MinimumScore { get; set; } = 0.45f;
}

public class RerankerOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://127.0.0.1:8081";
    public string Endpoint { get; set; } = "/rerank";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "jinaai/jina-reranker-v2-base-multilingual";
    public int TimeoutSeconds { get; set; } = 240;
    public int MaxDocumentCharacters { get; set; } = 2000;
    public bool FallbackToDenseOnError { get; set; } = true;
}
