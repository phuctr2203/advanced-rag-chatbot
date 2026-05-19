using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    [HttpPost]
    public IActionResult Chat([FromBody] ChatRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Chat pipeline starts in Phase 3." });
    }
}
