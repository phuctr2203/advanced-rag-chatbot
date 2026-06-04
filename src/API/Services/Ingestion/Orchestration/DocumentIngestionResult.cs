using PolicyBot.Api.Services.Ingestion.Storage;
using PolicyBot.Api.Services.Ingestion.Forms;

namespace PolicyBot.Api.Services.Ingestion.Orchestration;

public class DocumentIngestionResult
{
    public string FileName { get; set; } = string.Empty;
    public string Agent { get; set; } = Classification.DocumentClassifierService.DefaultAgent;
    public int ParsedChunkCount { get; set; }
    public int ChunkCount { get; set; }
    public StoredDocument? Document { get; set; }
    public StoredTemplate? Template { get; set; }
    public FormRegistrySuggestion? FormMappingSuggestion { get; set; }
}
