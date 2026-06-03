using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion.Classification;
using PolicyBot.Api.Services.Ingestion.Parsers;
using PolicyBot.Api.Services.Ingestion.Storage;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(
    PdfParserService pdfParserService,
    DocxParserService docxParserService,
    XlsxParserService xlsxParserService,
    FileConversionService fileConversionService,
    UploadedDocumentStorageService documentStorageService,
    FormTemplateDetectorService formTemplateDetectorService,
    TemplateStorageService templateStorageService) : ControllerBase
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
        var template = await StoreTemplateIfDetectedAsync(document.PhysicalPath, document.OriginalFileName, chunks, ct);
        return Ok(ToParseResponse(document, chunks, template));
    }

    [HttpPost("convert/doc")]
    public async Task<IActionResult> ConvertDoc(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var docxPath = await fileConversionService.ToDocxAsync(document.PhysicalPath, ct);
        var chunks = await docxParserService.ParseAsync(docxPath, document.OriginalFileName, ct: ct);
        var templateFileName = Path.ChangeExtension(document.OriginalFileName, ".docx");
        var template = await StoreTemplateIfDetectedAsync(docxPath, templateFileName, chunks, ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            template = ToTemplateResponse(template),
            docxPath,
            chunks
        });
    }

    [HttpPost("parse/xlsx")]
    public async Task<IActionResult> ParseXlsx(IFormFile file, CancellationToken ct)
    {
        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await xlsxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        var template = await StoreTemplateIfDetectedAsync(document.PhysicalPath, document.OriginalFileName, chunks, ct);
        return Ok(ToParseResponse(document, chunks, template));
    }

    private async Task<StoredTemplate?> StoreTemplateIfDetectedAsync(
        string sourcePath,
        string fileName,
        IReadOnlyList<ParsedChunk> chunks,
        CancellationToken ct)
    {
        var documentText = string.Join(' ', chunks.Where(chunk => chunk.ChunkType == "text").Select(chunk => chunk.Text));
        var detection = await formTemplateDetectorService.DetectAsync(fileName, documentText, ct);
        if (!detection.IsTemplate)
        {
            return null;
        }

        var template = await templateStorageService.SaveAsync(sourcePath, fileName, ct);
        foreach (var chunk in chunks)
        {
            chunk.IsFormTemplate = true;
            chunk.TemplatePath = template.UrlPath;
        }

        return template;
    }

    private static object ToParseResponse(StoredDocument document, IReadOnlyList<ParsedChunk> chunks, StoredTemplate? template = null)
    {
        return new
        {
            document = ToDocumentResponse(document),
            template = ToTemplateResponse(template),
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

    private static object? ToTemplateResponse(StoredTemplate? template)
    {
        if (template is null)
        {
            return null;
        }

        return new
        {
            originalFileName = template.OriginalFileName,
            storedFileName = template.StoredFileName,
            url = template.UrlPath,
            sha256 = template.Sha256,
            reused = template.AlreadyExisted
        };
    }
}
