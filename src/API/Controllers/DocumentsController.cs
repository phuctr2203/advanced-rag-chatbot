using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Services.Shared;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(DocumentLibraryService documentLibraryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return Ok(await documentLibraryService.ListAsync(ct));
    }

    [HttpDelete("{sourceFile}")]
    public async Task<IActionResult> Delete(string sourceFile, CancellationToken ct)
    {
        var deletedChunks = await documentLibraryService.DeleteAsync(sourceFile, ct);
        return Ok(new { sourceFile, deletedChunks });
    }
}
