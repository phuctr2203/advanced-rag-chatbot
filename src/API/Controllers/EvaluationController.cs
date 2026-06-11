using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/evaluation")]
public class EvaluationController(EvaluationChatOrchestrator orchestrator) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] EvaluationChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var response = await orchestrator.CompleteAsync(request, ct);
        return Ok(response);
    }
}
