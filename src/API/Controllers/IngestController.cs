using Microsoft.AspNetCore.Mvc;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    [HttpPost]
    public IActionResult Ingest(IFormFile file, [FromQuery] string? agent)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Ingestion pipeline starts in Phase 2." });
    }
}
