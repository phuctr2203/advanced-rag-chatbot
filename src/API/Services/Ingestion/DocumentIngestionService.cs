using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Services.Ingestion;

public class DocumentIngestionService(
    PdfParserService pdfParserService,
    DocxParserService docxParserService,
    XlsxParserService xlsxParserService,
    TextChunkerService textChunkerService,
    FileConversionService fileConversionService,
    DocumentClassifierService documentClassifierService,
    FormMentionExtractorService formMentionExtractorService,
    FormTemplateDetectorService formTemplateDetectorService,
    IEmbeddingProvider embeddingProvider,
    VectorStoreService vectorStoreService,
    ILogger<DocumentIngestionService> logger)
{
    public async Task<DocumentIngestionResult> IngestAsync(string filePath, string sourceFile, string? agent, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(agent) && !DocumentClassifierService.IsValidAgent(agent))
        {
            throw new ArgumentException("Invalid agent value. Valid values: ELCA_HR, ELCA_GENERAL, CII_TOWER_SUPPORT.", nameof(agent));
        }

        var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
        var parsedChunks = await ParseDocumentAsync(filePath, sourceFile, extension, ct);
        var resolvedAgent = await documentClassifierService.DetermineAgentAsync(parsedChunks, agent, ct);
        DocumentClassifierService.ApplyAgent(parsedChunks, resolvedAgent);

        if (extension == ".pdf")
        {
            await formMentionExtractorService.ExtractPdfFormMentionsAsync(sourceFile, parsedChunks, resolvedAgent, ct);
        }

        if (extension is ".docx" or ".doc")
        {
            var isFormTemplate = await formTemplateDetectorService.DetectAsync(sourceFile, parsedChunks, ct);
            FormTemplateDetectorService.ApplyFormTemplateTag(parsedChunks, isFormTemplate);
        }

        var chunks = textChunkerService.Chunk(parsedChunks);
        DocumentClassifierService.ApplyAgent(chunks, resolvedAgent);
        if (extension is ".docx" or ".doc")
        {
            FormTemplateDetectorService.ApplyFormTemplateTag(chunks, parsedChunks.Any(chunk => chunk.IsFormTemplate));
        }

        if (chunks.Count == 0)
        {
            logger.LogInformation("[DocumentIngestion] {FileName} produced no chunks after parsing and chunking.", sourceFile);
            return new DocumentIngestionResult
            {
                FileName = sourceFile,
                Agent = resolvedAgent,
                ChunkCount = 0
            };
        }

        var embeddings = await EmbedInBatchesAsync(chunks, ct);
        await vectorStoreService.UpsertAsync(chunks, embeddings, ct);

        logger.LogInformation(
            "[DocumentIngestion] Ingested {FileName} as {Agent} with {ChunkCount} chunks.",
            sourceFile,
            resolvedAgent,
            chunks.Count);

        return new DocumentIngestionResult
        {
            FileName = sourceFile,
            Agent = resolvedAgent,
            ChunkCount = chunks.Count
        };
    }

    private async Task<IReadOnlyList<ParsedChunk>> ParseDocumentAsync(string filePath, string sourceFile, string extension, CancellationToken ct)
    {
        return extension switch
        {
            ".pdf" => await pdfParserService.ParseAsync(filePath, sourceFile, ct: ct),
            ".pptx" => await pdfParserService.ParseAsync(await fileConversionService.ToPdfAsync(filePath, ct), sourceFile, ct: ct),
            ".docx" => await docxParserService.ParseAsync(filePath, sourceFile, ct: ct),
            ".doc" => await docxParserService.ParseAsync(await fileConversionService.ToDocxAsync(filePath, ct), sourceFile, ct: ct),
            ".xlsx" => await xlsxParserService.ParseAsync(filePath, sourceFile, ct: ct),
            _ => throw new NotSupportedException($"Unsupported file type '{Path.GetExtension(sourceFile)}'.")
        };
    }

    private async Task<List<float[]>> EmbedInBatchesAsync(IReadOnlyList<ParsedChunk> chunks, CancellationToken ct)
    {
        const int batchSize = 32;
        var embeddings = new List<float[]>(chunks.Count);

        for (var index = 0; index < chunks.Count; index += batchSize)
        {
            var batchTexts = chunks
                .Skip(index)
                .Take(batchSize)
                .Select(chunk => chunk.Text)
                .ToList();

            embeddings.AddRange(await embeddingProvider.EmbedAsync(batchTexts, ct));
        }

        return embeddings;
    }
}
