using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController(ProviderVisibilityService providerVisibilityService) : ControllerBase
{
    [HttpGet("current")]
    public IActionResult Current()
    {
        return Ok(providerVisibilityService.GetCurrent());
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        return Ok(await providerVisibilityService.GetStatusAsync(ct));
    }
}
