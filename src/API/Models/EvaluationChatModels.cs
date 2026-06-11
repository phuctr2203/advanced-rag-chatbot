namespace PolicyBot.Api.Models;

public class EvaluationChatRequest
{
    public string Message { get; set; } = string.Empty;
    public bool IncludePrompt { get; set; }
    public int? TopK { get; set; }
}

public class EvaluationChatResponse
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public LanguageDetectionResultDto Language { get; set; } = new();
    public string Intent { get; set; } = string.Empty;
    public IReadOnlyList<EvaluationRetrievedContext> RetrievedContexts { get; set; } = [];
    public IReadOnlyList<SourceRef> Sources { get; set; } = [];
    public IReadOnlyList<FormDownloadRef> FormDownloads { get; set; } = [];
    public EvaluationTimings TimingsMs { get; set; } = new();
    public string? Prompt { get; set; }
}

public class LanguageDetectionResultDto
{
    public string Language { get; set; } = "en";
    public string Method { get; set; } = "default";
    public float Confidence { get; set; }
}

public class EvaluationRetrievedContext
{
    public int Rank { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Score { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public int Page { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkType { get; set; } = "text";
    public string FileType { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public IReadOnlyList<string> ImagePaths { get; set; } = [];
    public bool IsFormTemplate { get; set; }
    public string TemplatePath { get; set; } = string.Empty;
}

public class EvaluationTimings
{
    public long LanguageDetection { get; set; }
    public long IntentClassification { get; set; }
    public long Retrieval { get; set; }
    public long Generation { get; set; }
    public long CitationParsing { get; set; }
    public long Total { get; set; }
}
