using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion.Images;
using PolicyBot.Api.Services.Ingestion.Ocr;
using UglyToad.PdfPig;

namespace PolicyBot.Api.Services.Ingestion.Parsers;

public class PdfParserService(
    PdfOcrService pdfOcrService,
    PdfImageExtractorService pdfImageExtractorService)
{
    public IReadOnlyList<PdfImageAnalysisResult> AnalyzeImages(string filePath)
    {
        var results = new List<PdfImageAnalysisResult>();

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            results.AddRange(pdfImageExtractorService.AnalyzeImages(page));
        }

        return results;
    }

    public async Task<IReadOnlyList<ParsedChunk>> ParseAsync(
        string filePath,
        string? sourceFile = null,
        string agent = "ELCA_GENERAL",
        CancellationToken ct = default)
    {
        var parseFilePath = await pdfOcrService.ResolveTextReadablePdfAsync(filePath, ct);
        var chunks = new List<ParsedChunk>();
        var fileName = sourceFile ?? Path.GetFileName(filePath);
        var docName = Path.GetFileNameWithoutExtension(fileName);
        var chunkIndex = 0;

        using var document = PdfDocument.Open(parseFilePath);
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var pageText = ExtractPageText(page);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                chunks.Add(new ParsedChunk
                {
                    Text = pageText,
                    SourceFile = fileName,
                    PageNumber = page.Number,
                    ChunkIndex = chunkIndex++,
                    ChunkType = "text",
                    FileType = "pdf",
                    Agent = agent
                });
            }

            var imageChunks = await pdfImageExtractorService.ExtractCaptionChunksAsync(
                page,
                docName,
                fileName,
                pageText,
                agent,
                chunkIndex,
                ct);

            chunks.AddRange(imageChunks);
            chunkIndex += imageChunks.Count;
        }

        return chunks;
    }

    private static string ExtractPageText(UglyToad.PdfPig.Content.Page page)
    {
        return string.Join(' ', page.GetWords().Select(word => word.Text));
    }
}
