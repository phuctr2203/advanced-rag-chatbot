using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Models;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(VectorStoreService vectorStoreService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentSummary>>> Get(CancellationToken ct)
    {
        var documents = await vectorStoreService.ListDocumentsAsync(ct);
        return Ok(documents);
    }

    [HttpDelete("{sourceFile}")]
    public async Task<IActionResult> Delete(string sourceFile, CancellationToken ct)
    {
        await vectorStoreService.DeleteDocumentAsync(sourceFile, ct);
        return NoContent();
    }
}
