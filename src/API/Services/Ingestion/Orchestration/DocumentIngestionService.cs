using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion.Chunking;
using PolicyBot.Api.Services.Ingestion.Classification;
using PolicyBot.Api.Services.Ingestion.Forms;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Ingestion.Storage;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Ingestion.Orchestration;

public class DocumentIngestionService(
    PdfParserService pdfParserService,
    DocxParserService docxParserService,
    XlsxParserService xlsxParserService,
    FileConversionService fileConversionService,
    DocumentClassifierService documentClassifierService,
    FormMentionExtractorService formMentionExtractorService,
    FormTemplateDetectorService formTemplateDetectorService,
    TemplateStorageService templateStorageService,
    TextChunkerService textChunkerService,
    IEmbeddingProvider embeddingProvider,
    IVectorStoreService vectorStoreService,
    ILogger<DocumentIngestionService> logger)
{
    public async Task<DocumentIngestionResult> IngestAsync(
        StoredDocument document,
        string? requestedAgent,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(requestedAgent) && !DocumentClassifierService.IsValidAgent(requestedAgent))
        {
            throw new ArgumentException(
                "Invalid agent. Valid values are ELCA_HR, ELCA_GENERAL, CII_TOWER_SUPPORT.",
                nameof(requestedAgent));
        }

        var extension = Path.GetExtension(document.OriginalFileName).ToLowerInvariant();
        var parseResult = await ParseDocumentAsync(document, extension, ct);
        var resolvedAgent = await documentClassifierService.DetermineAgentAsync(parseResult.Chunks, requestedAgent, ct);
        DocumentClassifierService.ApplyAgent(parseResult.Chunks, resolvedAgent);

        if (extension == ".pdf")
        {
            await formMentionExtractorService.ExtractPdfFormMentionsAsync(
                document.OriginalFileName,
                parseResult.Chunks,
                resolvedAgent,
                ct);
        }

        var template = await StoreTemplateIfDetectedAsync(
            parseResult.TemplateCandidatePath,
            parseResult.TemplateCandidateFileName,
            parseResult.Chunks,
            ct);

        var chunks = textChunkerService.Chunk(parseResult.Chunks);
        DocumentClassifierService.ApplyAgent(chunks, resolvedAgent);
        ApplyTemplateMetadata(chunks, template);

        if (chunks.Count == 0)
        {
            logger.LogInformation(
                "[DocumentIngestion] {FileName} produced no chunks after parsing and chunking.",
                document.OriginalFileName);

            return new DocumentIngestionResult
            {
                FileName = document.OriginalFileName,
                Agent = resolvedAgent,
                ParsedChunkCount = parseResult.Chunks.Count,
                ChunkCount = 0,
                Document = document,
                Template = template
            };
        }

        var embeddings = await embeddingProvider.EmbedAsync(chunks.Select(chunk => chunk.Text).ToList(), ct);
        await vectorStoreService.UpsertAsync(chunks, embeddings, ct);

        logger.LogInformation(
            "[DocumentIngestion] Ingested {FileName} as {Agent} with {ChunkCount} chunks.",
            document.OriginalFileName,
            resolvedAgent,
            chunks.Count);

        return new DocumentIngestionResult
        {
            FileName = document.OriginalFileName,
            Agent = resolvedAgent,
            ParsedChunkCount = parseResult.Chunks.Count,
            ChunkCount = chunks.Count,
            Document = document,
            Template = template
        };
    }

    private async Task<DocumentParseResult> ParseDocumentAsync(StoredDocument document, string extension, CancellationToken ct)
    {
        return extension switch
        {
            ".pdf" => new DocumentParseResult
            {
                Chunks = await pdfParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct)
            },
            ".pptx" => new DocumentParseResult
            {
                Chunks = await pdfParserService.ParseAsync(
                    await fileConversionService.ToPdfAsync(document.PhysicalPath, ct),
                    document.OriginalFileName,
                    ct: ct)
            },
            ".docx" => new DocumentParseResult
            {
                Chunks = await docxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct),
                TemplateCandidatePath = document.PhysicalPath,
                TemplateCandidateFileName = document.OriginalFileName
            },
            ".doc" => await ParseConvertedDocAsync(document, ct),
            ".xlsx" => new DocumentParseResult
            {
                Chunks = await xlsxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct),
                TemplateCandidatePath = document.PhysicalPath,
                TemplateCandidateFileName = document.OriginalFileName
            },
            _ => throw new NotSupportedException($"Unsupported file type '{Path.GetExtension(document.OriginalFileName)}'.")
        };
    }

    private async Task<DocumentParseResult> ParseConvertedDocAsync(StoredDocument document, CancellationToken ct)
    {
        var docxPath = await fileConversionService.ToDocxAsync(document.PhysicalPath, ct);
        var templateFileName = Path.ChangeExtension(document.OriginalFileName, ".docx");

        return new DocumentParseResult
        {
            Chunks = await docxParserService.ParseAsync(docxPath, document.OriginalFileName, ct: ct),
            TemplateCandidatePath = docxPath,
            TemplateCandidateFileName = templateFileName
        };
    }

    private async Task<StoredTemplate?> StoreTemplateIfDetectedAsync(
        string? sourcePath,
        string? fileName,
        IReadOnlyList<ParsedChunk> chunks,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var documentText = string.Join(' ', chunks.Where(chunk => chunk.ChunkType == "text").Select(chunk => chunk.Text));
        var detection = await formTemplateDetectorService.DetectAsync(fileName, documentText, ct);
        if (!detection.IsTemplate)
        {
            return null;
        }

        var template = await templateStorageService.SaveAsync(sourcePath, fileName, ct);
        ApplyTemplateMetadata(chunks, template);
        return template;
    }

    private static void ApplyTemplateMetadata(IReadOnlyList<ParsedChunk> chunks, StoredTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        foreach (var chunk in chunks)
        {
            chunk.IsFormTemplate = true;
            chunk.TemplatePath = template.UrlPath;
        }
    }

    private class DocumentParseResult
    {
        public IReadOnlyList<ParsedChunk> Chunks { get; set; } = [];
        public string? TemplateCandidatePath { get; set; }
        public string? TemplateCandidateFileName { get; set; }
    }
}
