using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Ingestion.Chunking;
using PolicyBot.Api.Services.Ingestion.Classification;
using PolicyBot.Api.Services.Ingestion.Orchestration;
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
    DocumentClassifierService documentClassifierService,
    DocumentIngestionService documentIngestionService,
    TextChunkerService textChunkerService,
    TemplateStorageService templateStorageService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest(
        IFormFile file,
        [FromQuery] string? agent,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "Upload a non-empty document." });
        }

        if (!IsSupportedDocument(file.FileName))
        {
            return BadRequest(new { error = $"Unsupported file type '{Path.GetExtension(file.FileName)}'." });
        }

        try
        {
            var document = await documentStorageService.SaveAsync(file, ct);
            var result = await documentIngestionService.IngestAsync(document, agent, force, ct);

            return Ok(ToIngestResponse(result, document));
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ArgumentException exception) when (exception.ParamName is "requestedAgent" or "agent")
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> BatchIngest(
        [FromForm] List<IFormFile>? files,
        [FromQuery] string? agent,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        if (files is null || files.Count == 0)
        {
            return BadRequest(new { error = "Upload at least one document." });
        }

        var results = new List<object>();
        var succeeded = 0;
        var failed = 0;

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                failed++;
                results.Add(new { fileName = file.FileName, error = "Upload a non-empty document." });
                continue;
            }

            if (!IsSupportedDocument(file.FileName))
            {
                failed++;
                results.Add(new { fileName = file.FileName, error = $"Unsupported file type '{Path.GetExtension(file.FileName)}'." });
                continue;
            }

            try
            {
                var document = await documentStorageService.SaveAsync(file, ct);
                var result = await documentIngestionService.IngestAsync(document, agent, force, ct);
                succeeded++;
                results.Add(ToIngestResponse(result, document));
            }
            catch (NotSupportedException exception)
            {
                failed++;
                results.Add(new { fileName = file.FileName, error = exception.Message });
            }
            catch (ArgumentException exception) when (exception.ParamName is "requestedAgent" or "agent")
            {
                failed++;
                results.Add(new { fileName = file.FileName, error = exception.Message });
            }
        }

        return Ok(new
        {
            message = $"Processed {files.Count} document(s).",
            succeeded,
            failed,
            results
        });
    }

    [HttpPost("parse/pdf")]
    public async Task<IActionResult> ParsePdf(IFormFile file, [FromQuery] string? agent, CancellationToken ct)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await pdfParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        var resolvedAgent = await ResolveAgentAndApplyAsync(chunks, agent, ct);
        return Ok(ToParseResponse(document, chunks, agent: resolvedAgent));
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
    public async Task<IActionResult> ConvertPptx(IFormFile file, [FromQuery] string? agent, CancellationToken ct)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        var document = await documentStorageService.SaveAsync(file, ct);
        var pdfPath = await fileConversionService.ToPdfAsync(document.PhysicalPath, ct);
        var chunks = await pdfParserService.ParseAsync(pdfPath, document.OriginalFileName, ct: ct);
        var resolvedAgent = await ResolveAgentAndApplyAsync(chunks, agent, ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            agent = resolvedAgent,
            pdfPath,
            pages = chunks.Select(chunk => chunk.PageNumber).Distinct().Count(),
            chunks
        });
    }

    [HttpPost("parse/docx")]
    public async Task<IActionResult> ParseDocx(IFormFile file, [FromQuery] string? agent, CancellationToken ct)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await docxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        var resolvedAgent = await ResolveAgentAndApplyAsync(chunks, agent, ct);
        var template = await StoreTemplateIfDetectedAsync(document.PhysicalPath, document.OriginalFileName, chunks, ct);
        return Ok(ToParseResponse(document, chunks, template, resolvedAgent));
    }

    [HttpPost("convert/doc")]
    public async Task<IActionResult> ConvertDoc(IFormFile file, [FromQuery] string? agent, CancellationToken ct)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        var document = await documentStorageService.SaveAsync(file, ct);
        var docxPath = await fileConversionService.ToDocxAsync(document.PhysicalPath, ct);
        var chunks = await docxParserService.ParseAsync(docxPath, document.OriginalFileName, ct: ct);
        var resolvedAgent = await ResolveAgentAndApplyAsync(chunks, agent, ct);
        var templateFileName = Path.ChangeExtension(document.OriginalFileName, ".docx");
        var template = await StoreTemplateIfDetectedAsync(docxPath, templateFileName, chunks, ct);
        return Ok(new
        {
            document = ToDocumentResponse(document),
            template = ToTemplateResponse(template),
            agent = resolvedAgent,
            docxPath,
            chunks
        });
    }

    [HttpPost("parse/xlsx")]
    public async Task<IActionResult> ParseXlsx(IFormFile file, [FromQuery] string? agent, CancellationToken ct)
    {
        if (InvalidAgentResponse(agent) is { } invalidAgent)
        {
            return invalidAgent;
        }

        var document = await documentStorageService.SaveAsync(file, ct);
        var chunks = await xlsxParserService.ParseAsync(document.PhysicalPath, document.OriginalFileName, ct: ct);
        var resolvedAgent = await ResolveAgentAndApplyAsync(chunks, agent, ct);
        var template = await StoreTemplateIfDetectedAsync(document.PhysicalPath, document.OriginalFileName, chunks, ct);
        return Ok(ToParseResponse(document, chunks, template, resolvedAgent));
    }

    [HttpPost("chunk")]
    public ActionResult<IReadOnlyList<ParsedChunk>> Chunk(IReadOnlyList<ParsedChunk> chunks)
    {
        return Ok(textChunkerService.Chunk(chunks));
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

    [HttpPost("chunk/sentence-window")]
    public ActionResult<IReadOnlyList<ParsedChunk>> ChunkSentenceWindow(IReadOnlyList<ParsedChunk> chunks)
    {
        return Ok(textChunkerService.ChunkSentenceWindow(chunks));
    }

    [HttpPost("chunk/recursive-boundary")]
    public ActionResult<IReadOnlyList<ParsedChunk>> ChunkRecursiveBoundary(IReadOnlyList<ParsedChunk> chunks)
    {
        return Ok(textChunkerService.ChunkRecursiveBoundary(chunks));
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

    private async Task<string> ResolveAgentAndApplyAsync(IReadOnlyList<ParsedChunk> chunks, string? requestedAgent, CancellationToken ct)
    {
        var resolvedAgent = await documentClassifierService.DetermineAgentAsync(chunks, requestedAgent, ct);
        DocumentClassifierService.ApplyAgent(chunks, resolvedAgent);
        return resolvedAgent;
    }

    private BadRequestObjectResult? InvalidAgentResponse(string? agent)
    {
        if (string.IsNullOrWhiteSpace(agent) || DocumentClassifierService.IsValidAgent(agent))
        {
            return null;
        }

        return BadRequest(new { error = "Invalid agent. Valid values are ELCA_HR, ELCA_GENERAL, CII_TOWER_SUPPORT." });
    }

    private static bool IsSupportedDocument(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() is ".pdf" or ".pptx" or ".docx" or ".doc" or ".xlsx";
    }

    private static object ToParseResponse(
        StoredDocument document,
        IReadOnlyList<ParsedChunk> chunks,
        StoredTemplate? template = null,
        string? agent = null)
    {
        return new
        {
            document = ToDocumentResponse(document),
            template = ToTemplateResponse(template),
            agent = agent ?? chunks.FirstOrDefault()?.Agent ?? DocumentClassifierService.DefaultAgent,
            chunks
        };
    }

    private static object ToIngestResponse(DocumentIngestionResult result, StoredDocument document)
    {
        return new
        {
            fileName = result.FileName,
            message = result.Skipped
                ? $"{result.FileName} unchanged - skipped"
                : result.IsUpdate
                    ? $"{result.FileName} updated successfully"
                    : $"{result.FileName} ingested successfully",
            agent = result.Agent,
            chunks = result.ChunkCount,
            replacedChunks = result.ReplacedChunks,
            isUpdate = result.IsUpdate,
            skipped = result.Skipped,
            fileHash = result.FileHash,
            ingestedAt = result.IngestedAt,
            document = ToDocumentResponse(document),
            template = ToTemplateResponse(result.Template),
            formMappingSuggestion = result.FormMappingSuggestion
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
