using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController(ProviderStatusService providerStatusService) : ControllerBase
{
    [HttpGet("current")]
    public ActionResult<CurrentProviderResponse> GetCurrent()
    {
        return Ok(providerStatusService.GetCurrent());
    }

    [HttpGet("status")]
    public async Task<ActionResult<ProviderStatusResponse>> GetStatus(CancellationToken ct)
    {
        return Ok(await providerStatusService.GetStatusAsync(ct));
    }
}
