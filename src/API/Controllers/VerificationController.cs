using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Ingestion.Storage;
using PolicyBot.Api.Services.Query;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("verify")]
public class VerificationController(
    ILlmProvider llmProvider,
    IVisionProvider visionProvider,
    IEmbeddingProvider embeddingProvider,
    IVectorStoreService vectorStoreService,
    LanguageDetectionService languageDetectionService,
    IntentClassifierService intentClassifierService,
    PromptBuilderService promptBuilderService,
    LlmService llmService,
    SourceCitationParser sourceCitationParser,
    FormRegistryService formRegistryService,
    FormDownloadEnrichmentService formDownloadEnrichmentService,
    ConfiguredPathResolver pathResolver) : ControllerBase
{
    [HttpPost("llm")]
    public async Task<IActionResult> VerifyLlm(CancellationToken ct)
    {
        var response = await llmProvider.CompleteAsync("Reply with exactly one word: OK", maxTokens: 10, ct);
        return Ok(new { response });
    }

    [HttpPost("vision")]
    public async Task<IActionResult> VerifyVision(IFormFile image, CancellationToken ct)
    {
        await using var stream = image.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);

        var description = await visionProvider.DescribeImageAsync(memory.ToArray(), "Phase 1 verification image.", ct: ct);
        return Ok(new { description, passed = !string.IsNullOrWhiteSpace(description) });
    }

    [HttpPost("embedding")]
    public async Task<IActionResult> VerifyEmbedding(CancellationToken ct)
    {
        var vectors = await embeddingProvider.EmbedAsync(
            ["What is the annual leave policy?", "Chinh sach nghi phep hang nam la gi?"],
            ct);

        return Ok(new
        {
            count = vectors.Count,
            dimensions = vectors.Select(vector => vector.Length).ToArray(),
            passed = vectors.Count == 2 && vectors.All(vector => vector.Length == 1024)
        });
    }

    [HttpPost("vector-roundtrip")]
    public async Task<IActionResult> VerifyVectorRoundTrip(CancellationToken ct)
    {
        var vectors = await embeddingProvider.EmbedAsync(["Annual leave policy verification chunk."], ct);
        var passed = vectors.Count == 1 && await ((VectorStoreService)vectorStoreService).VerifyRoundTripAsync(vectors[0], ct);
        return Ok(new { passed });
    }

    [HttpPost("query-intent")]
    public async Task<IActionResult> VerifyQueryIntent([FromBody] QueryIntentVerificationRequest request, CancellationToken ct)
    {
        var language = await languageDetectionService.DetectAsync(request.Message, ct);
        var intent = await intentClassifierService.ClassifyAsync(request.Message, ct);
        var response = intent switch
        {
            QueryIntent.Smalltalk => IntentResponses.Smalltalk(language.Language),
            QueryIntent.OutOfScope => IntentResponses.OutOfScope(language.Language),
            _ => IntentResponses.NoResults(language.Language)
        };

        return Ok(new
        {
            language,
            intent = intent.ToString(),
            response
        });
    }

    [HttpPost("vector-search")]
    public async Task<IActionResult> VerifyVectorSearch([FromBody] VectorSearchVerificationRequest request, CancellationToken ct)
    {
        var limit = request.Limit is > 0 and <= 20 ? request.Limit : 6;
        var results = await vectorStoreService.SearchAsync(request.Query, limit, ct);

        return Ok(new
        {
            count = results.Count,
            passed = results.Count > 0 && results.Count <= limit,
            results = results.Select(result => new
            {
                score = result.Score,
                text = result.Chunk.Text,
                sourceFile = result.Chunk.SourceFile,
                pageNumber = result.Chunk.PageNumber,
                chunkIndex = result.Chunk.ChunkIndex,
                chunkType = result.Chunk.ChunkType,
                fileType = result.Chunk.FileType,
                agent = result.Chunk.Agent,
                imagePath = result.Chunk.ImagePath,
                imagePaths = result.Chunk.ImagePaths,
                isFormTemplate = result.Chunk.IsFormTemplate,
                templatePath = result.Chunk.TemplatePath
            })
        });
    }

    [HttpPost("prompt-builder")]
    public IActionResult VerifyPromptBuilder([FromBody] PromptBuilderVerificationRequest request)
    {
        var chunks = new List<ScoredChunk>
        {
            new()
            {
                Score = 0.82f,
                Chunk = new ParsedChunk
                {
                    Text = "Employees must follow the CII Tower fire alarm and evacuation procedure.",
                    SourceFile = "emergency-handbook.pdf",
                    PageNumber = 7,
                    ChunkIndex = 0,
                    ChunkType = "text",
                    FileType = "pdf",
                    Agent = "CII_TOWER_SUPPORT"
                }
            }
        };

        var prompt = promptBuilderService.Build(request.Query, chunks, request.Language);
        return Ok(new
        {
            prompt,
            passed =
                prompt.Contains("Answer ONLY based on the provided context.", StringComparison.Ordinal)
                && prompt.Contains("SOURCES: filename.pdf (page N)", StringComparison.Ordinal)
                && prompt.Contains("Source: emergency-handbook.pdf (page 7)", StringComparison.Ordinal)
                && prompt.Contains(request.Query, StringComparison.Ordinal)
        });
    }

    [HttpPost("llm-service")]
    public async Task<IActionResult> VerifyLlmService(CancellationToken ct)
    {
        var completion = await llmService.CompleteAsync("Reply with exactly one word: OK", maxTokens: 10, ct);
        var streamed = new List<string>();
        await foreach (var token in llmService.StreamAsync("Reply with exactly one word: OK", ct))
        {
            streamed.Add(token);
            if (string.Concat(streamed).Length >= 20)
            {
                break;
            }
        }

        return Ok(new
        {
            completion,
            streamPreview = string.Concat(streamed),
            passed = !string.IsNullOrWhiteSpace(completion) && streamed.Count > 0
        });
    }

    [HttpPost("source-citations")]
    public IActionResult VerifySourceCitations()
    {
        var response = "Use the fire hose cabinet during fire response.\nSOURCES: handbook.pdf (page 8), payment-form.docx (page 1)";
        var searchResults = new List<ScoredChunk>
        {
            new()
            {
                Score = 0.75f,
                Chunk = new ParsedChunk
                {
                    Text = "The image shows a red emergency fire hose cabinet.",
                    SourceFile = "handbook.pdf",
                    PageNumber = 8,
                    ChunkIndex = 1,
                    ChunkType = "image_caption",
                    FileType = "pdf",
                    Agent = "CII_TOWER_SUPPORT",
                    ImagePath = "/images/handbook/page8_img2.jpg"
                }
            },
            new()
            {
                Score = 0.71f,
                Chunk = new ParsedChunk
                {
                    Text = "Payment request form template.",
                    SourceFile = "payment-form.docx",
                    PageNumber = 1,
                    ChunkIndex = 0,
                    ChunkType = "text",
                    FileType = "docx",
                    Agent = "ELCA_GENERAL",
                    IsFormTemplate = true,
                    TemplatePath = "/templates/payment-form.docx"
                }
            }
        };

        var sources = sourceCitationParser.Parse(response, searchResults);
        return Ok(new
        {
            sources,
            passed =
                sources.Count == 2
                && sources.Any(source => source.ChunkType == "image_caption" && source.ImagePath == "/images/handbook/page8_img2.jpg")
                && sources.Any(source => source.FormDownload?.DownloadPath == "/templates/payment-form.docx")
        });
    }

    [HttpPost("form-registry")]
    public IActionResult VerifyFormRegistry()
    {
        var registryPath = WriteTemporaryRegistry();
        try
        {
            formRegistryService.Load(registryPath);
            var byFile = formRegistryService.FindByFile("payment-request-template.docx");
            var byAlias = formRegistryService.FindByAlias("Please use the Payment Request Form for this process.");

            return Ok(new
            {
                byFile,
                byAlias,
                passed =
                    byFile?.DownloadPath == "/templates/payment-request-template.docx"
                    && byAlias?.FormName == "Payment Request Form"
            });
        }
        finally
        {
            formRegistryService.Load(Path.Combine(pathResolver.DataPath, "form-registry.json"));
        }
    }

    [HttpPost("form-download-enrichment")]
    public IActionResult VerifyFormDownloadEnrichment()
    {
        var registryPath = WriteTemporaryRegistry();
        try
        {
            formRegistryService.Load(registryPath);
            var response = "Submit the Payment Request Form.\nSOURCES: payment-request-template.docx (page 1)";
            var searchResults = new List<ScoredChunk>
            {
                new()
                {
                    Score = 0.81f,
                    Chunk = new ParsedChunk
                    {
                        Text = "Payment request form template.",
                        SourceFile = "payment-request-template.docx",
                        PageNumber = 1,
                        ChunkIndex = 0,
                        ChunkType = "text",
                        FileType = "docx",
                        Agent = "ELCA_GENERAL",
                        IsFormTemplate = true,
                        TemplatePath = "/templates/fallback-payment-request-template.docx"
                    }
                }
            };

            var sources = sourceCitationParser.Parse(response, searchResults);
            var formDownloads = formDownloadEnrichmentService.FindDownloadRefs(response, []);
            var duplicateDownloads = formDownloadEnrichmentService.FindDownloadRefs(response, sources);

            return Ok(new
            {
                sources,
                formDownloads,
                duplicateDownloads,
                passed =
                    sources.SingleOrDefault()?.FormDownload?.DownloadPath == "/templates/payment-request-template.docx"
                    && formDownloads.SingleOrDefault()?.FormName == "Payment Request Form"
                    && duplicateDownloads.Count == 0
            });
        }
        finally
        {
            formRegistryService.Load(Path.Combine(pathResolver.DataPath, "form-registry.json"));
        }
    }

    private string WriteTemporaryRegistry()
    {
        Directory.CreateDirectory(pathResolver.TempPath);
        var registryPath = Path.Combine(pathResolver.TempPath, "verify-form-registry.json");
        System.IO.File.WriteAllText(registryPath, """
            [
              {
                "form_name": "Payment Request Form",
                "aliases": ["Payment Request Form", "payment request", "payment form"],
                "docx_file": "payment-request-template.docx",
                "download_path": "/templates/payment-request-template.docx",
                "agent": "ELCA_GENERAL"
              }
            ]
            """);

        return registryPath;
    }
}

public class QueryIntentVerificationRequest
{
    public string Message { get; set; } = string.Empty;
}

public class VectorSearchVerificationRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 6;
}

public class PromptBuilderVerificationRequest
{
    public string Query { get; set; } = "What is the fire alarm procedure?";
    public string Language { get; set; } = "en";
}
