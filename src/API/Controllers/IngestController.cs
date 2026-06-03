using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion;
using PolicyBot.Api.Services.Ingestion.Parsers;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(
    PdfParserService pdfParserService,
    DocxParserService docxParserService,
    XlsxParserService xlsxParserService,
    FileConversionService fileConversionService,
    UploadedDocumentStorageService documentStorageService) : ControllerBase
{
    [HttpPost]
    public IActionResult Ingest(IFormFile file, [FromQuery] string? agent)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Full ingestion pipeline starts in Phase 2.13." });
    }

    [HttpPost("parse/pdf")]
    public async Task<IActionResult> ParsePdf(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await pdfParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        return Ok(ToParseResponse(document, chunks));
    }

    [HttpPost("analyze/pdf-images")]
    public async Task<IActionResult> AnalyzePdfImages(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            images = pdfParserService.AnalyzeImages(document.PhysicalPath)
        });
    }

    [HttpPost("convert/pptx")]
    public async Task<IActionResult> ConvertPptx(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var pdfPath = await fileConversionService.ToPdfAsync(document.PhysicalPath, ct);
        var chunks = await pdfParserService.ParseAsync(pdfPath, document.OriginalFileName, ct: ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            pdfPath,
            pages = chunks.Select(chunk => chunk.PageNumber).Distinct().Count(),
            chunks
        });
    }

    [HttpPost("parse/docx")]
    public async Task<IActionResult> ParseDocx(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await docxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        return Ok(ToParseResponse(document, chunks));
    }

    [HttpPost("convert/doc")]
    public async Task<IActionResult> ConvertDoc(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var docxPath = await fileConversionService.ToDocxAsync(document.PhysicalPath, ct);
        var chunks = await docxParserService.ParseAsync(docxPath, document.OriginalFileName, ct: ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            docxPath,
            chunks
        });
    }

    [HttpPost("parse/xlsx")]
    public async Task<IActionResult> ParseXlsx(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await xlsxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        return Ok(ToParseResponse(document, chunks));
    }

    private static object ToParseResponse(StoredDocument document, IReadOnlyList<ParsedChunk> chunks)
    {
        return new
        {
            document = ToDocumentResponse(document),
            chunks
        };
    }

    private static object ToDocumentResponse(StoredDocument document)
    {
        return new
        {
            originalFileName = document.OriginalFileName,
            storedFileName = document.StoredFileName,
            url = document.UrlPath,
            sha256 = document.Sha256,
            reused = document.AlreadyExisted
        };
    }
}
