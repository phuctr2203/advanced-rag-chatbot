using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("verify")]
public class VerificationController(
    ILlmProvider llmProvider,
    IVisionProvider visionProvider,
    IEmbeddingProvider embeddingProvider,
    IVectorStoreService vectorStoreService) : ControllerBase
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
}
