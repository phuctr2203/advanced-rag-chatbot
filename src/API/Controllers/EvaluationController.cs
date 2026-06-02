using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PolicyBot.Api.Models;
using PolicyBot.Api.Options;
using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/evaluation")]
public class EvaluationController(
    IOptions<EvaluationOptions> options,
    EvaluationQueryService evaluationQueryService) : ControllerBase
{
    [HttpPost("query")]
    public async Task<ActionResult<EvaluationQueryResponse>> Query(
        [FromBody] EvaluationQueryRequest request,
        CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question is required." });
        }

        return Ok(await evaluationQueryService.QueryAsync(request.Question, ct));
    }
}
