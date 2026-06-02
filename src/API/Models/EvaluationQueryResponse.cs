namespace PolicyBot.Api.Models;

public class EvaluationQueryResponse
{
    public string Question { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string LanguageDetectionMethod { get; set; } = string.Empty;
    public float LanguageDetectionConfidence { get; set; }
    public string Intent { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool RetrievalRan { get; set; }
    public bool NoAnswer { get; set; }
    public List<EvaluationRetrievedContext> RetrievedContexts { get; set; } = [];
    public List<SourceRef> Sources { get; set; } = [];
    public List<FormDownloadRef> FormDownloads { get; set; } = [];
}

public class EvaluationRetrievedContext
{
    public string Text { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int Page { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkType { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public bool IsFormTemplate { get; set; }
    public float Score { get; set; }
}
