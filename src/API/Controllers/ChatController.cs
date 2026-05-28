using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(ChatOrchestrator orchestrator) : ControllerBase
{
    [HttpPost]
    public async Task Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        await foreach (var token in orchestrator.StreamAsync(request, ct))
        {
            await WriteSseDataAsync(token, ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    private async Task WriteSseDataAsync(string data, CancellationToken ct)
    {
        var lines = data.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            await Response.WriteAsync($"data: {line}\n", ct);
        }

        await Response.WriteAsync("\n", ct);
    }
}
