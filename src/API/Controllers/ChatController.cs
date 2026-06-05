using System.Text;
using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Query;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api")]
public class ChatController(ChatOrchestrator orchestrator) : ControllerBase
{
    [HttpPost("chat")]
    public async Task Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseDataAsync("Message is required.", ct);
            return;
        }

        await foreach (var token in orchestrator.StreamAsync(request, ct))
        {
            await WriteSseDataAsync(token, ct);
        }
    }

    private async Task WriteSseDataAsync(string data, CancellationToken ct)
    {
        var builder = new StringBuilder();
        foreach (var line in data.Replace("\r\n", "\n").Split('\n'))
        {
            builder.Append("data: ");
            builder.AppendLine(line);
        }

        builder.AppendLine();
        await Response.WriteAsync(builder.ToString(), ct);
        await Response.Body.FlushAsync(ct);
    }
}
