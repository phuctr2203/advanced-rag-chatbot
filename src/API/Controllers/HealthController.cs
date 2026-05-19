using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Providers;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(
    IEmbeddingProvider embeddingProvider,
    ILlmProvider llmProvider,
    VectorStoreService vectorStoreService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }

    [HttpPost("embedding")]
    public async Task<IActionResult> VerifyEmbedding(CancellationToken ct)
    {
        var embeddings = await embeddingProvider.EmbedAsync([
            "What is the annual leave policy?",
            "Chính sách nghỉ phép hàng năm là gì?"
        ], ct);

        return Ok(new
        {
            count = embeddings.Count,
            dimensions = embeddings.Select(embedding => embedding.Length).ToArray()
        });
    }

    [HttpPost("vector-store")]
    public async Task<IActionResult> VerifyVectorStore(CancellationToken ct)
    {
        var embeddings = await embeddingProvider.EmbedAsync(["Annual leave policy verification chunk."], ct);
        var success = await vectorStoreService.VerifyRoundTripAsync(embeddings[0], ct);
        return Ok(new { success });
    }

    [HttpPost("llm")]
    public async Task<IActionResult> VerifyLlm(CancellationToken ct)
    {
        var response = await llmProvider.CompleteAsync("Reply with OK only.", 10, ct);
        return Ok(new { response = response.Trim() });
    }
}
