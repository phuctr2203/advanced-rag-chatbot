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
    LanguageDetectionService languageDetectionService,
    IntentClassifierService intentClassifierService,
    LlmService llmService,
    HybridSearchService hybridSearchService) : ControllerBase
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

    [HttpPost("hybrid-search")]
    public async Task<IActionResult> VerifyHybridSearch([FromBody] VectorSearchVerificationRequest request, CancellationToken ct)
    {
        var diagnostics = await hybridSearchService.CompareAsync(request.Query, ct);

        return Ok(new
        {
            diagnostics.Query,
            dense = ToSearchResultResponse(diagnostics.Dense),
            keyword = ToSearchResultResponse(diagnostics.Keyword),
            hybrid = ToSearchResultResponse(diagnostics.Hybrid),
            passed = diagnostics.Hybrid.Count > 0
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

    private static IEnumerable<object> ToSearchResultResponse(IReadOnlyList<ScoredChunk> results)
    {
        return results.Select(result => new
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
        });
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
