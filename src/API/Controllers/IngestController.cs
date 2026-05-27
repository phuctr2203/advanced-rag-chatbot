using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion;
using PolicyBot.Api.Services.Ingestion.Parsers;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(PdfParserService pdfParserService, DocxParserService docxParserService, XlsxParserService xlsxParserService, TextChunkerService textChunkerService, FileConversionService fileConversionService) : ControllerBase
{
    [HttpPost]
    public IActionResult Ingest(IFormFile file, [FromQuery] string? agent)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Full ingestion pipeline starts in Phase 2.13." });
    }

    [HttpPost("parse/pdf")]
    public async Task<ActionResult<IReadOnlyList<ParsedChunk>>> ParsePdf(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        var chunks = await pdfParserService.ParseAsync(filePath, file.FileName, ct: ct);
        return Ok(chunks);
    }

    [HttpPost("analyze/pdf-images")]
    public async Task<IActionResult> AnalyzePdfImages(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        return Ok(pdfParserService.AnalyzeImages(filePath));
    }

    [HttpPost("convert/pptx")]
    public async Task<IActionResult> ConvertPptx(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        var pdfPath = await fileConversionService.ToPdfAsync(filePath, ct);
        var chunks = await pdfParserService.ParseAsync(pdfPath, file.FileName, ct: ct);
        return Ok(new
        {
            pdfPath,
            pages = chunks.Select(chunk => chunk.PageNumber).Distinct().Count(),
            chunks
        });
    }

    [HttpPost("parse/docx")]
    public async Task<ActionResult<IReadOnlyList<ParsedChunk>>> ParseDocx(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        var chunks = await docxParserService.ParseAsync(filePath, file.FileName, ct: ct);
        return Ok(chunks);
    }

    [HttpPost("convert/doc")]
    public async Task<IActionResult> ConvertDoc(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        var docxPath = await fileConversionService.ToDocxAsync(filePath, ct);
        var chunks = await docxParserService.ParseAsync(docxPath, file.FileName, ct: ct);
        return Ok(new
        {
            docxPath,
            chunks
        });
    }

    [HttpPost("parse/xlsx")]
    public async Task<ActionResult<IReadOnlyList<ParsedChunk>>> ParseXlsx(IFormFile file, CancellationToken ct)
    {
        var filePath = await SaveTempFileAsync(file, ct);
        var chunks = await xlsxParserService.ParseAsync(filePath, file.FileName, ct: ct);
        return Ok(chunks);
    }

    [HttpPost("chunk/fixed-size")]
    public ActionResult<IReadOnlyList<ParsedChunk>> ChunkFixedSize(IReadOnlyList<ParsedChunk> chunks)
    {
        return Ok(textChunkerService.ChunkFixedSize(chunks));
    }

    [HttpPost("chunk/paragraph-boundary")]
    public ActionResult<IReadOnlyList<ParsedChunk>> ChunkParagraphBoundary(IReadOnlyList<ParsedChunk> chunks)
    {
        return Ok(textChunkerService.ChunkParagraphBoundary(chunks));
    }

    private static async Task<string> SaveTempFileAsync(IFormFile file, CancellationToken ct)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "policy-bot-uploads");
        Directory.CreateDirectory(tempDirectory);

        var safeFileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}_{safeFileName}");
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream, ct);
        return filePath;
    }
}
