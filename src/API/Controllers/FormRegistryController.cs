using Microsoft.AspNetCore.Mvc;
using PolicyBot.Api.Services.Ingestion.Forms;

namespace PolicyBot.Api.Controllers;

[ApiController]
[Route("api/form-registry")]
public class FormRegistryController(FormRegistrySuggestionService suggestionService) : ControllerBase
{
    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<FormRegistrySuggestion>>> GetSuggestions(CancellationToken ct)
    {
        return Ok(await suggestionService.GetSuggestionsAsync(ct));
    }

    [HttpPost("suggestions/{id}/accept")]
    public async Task<ActionResult<FormRegistrySuggestion>> AcceptSuggestion(string id, CancellationToken ct)
    {
        var suggestion = await suggestionService.AcceptAsync(id, ct);
        return suggestion is null ? NotFound(new { error = "Suggestion not found." }) : Ok(suggestion);
    }

    [HttpPost("suggestions/{id}/reject")]
    public async Task<ActionResult<FormRegistrySuggestion>> RejectSuggestion(string id, CancellationToken ct)
    {
        var suggestion = await suggestionService.RejectAsync(id, ct);
        return suggestion is null ? NotFound(new { error = "Suggestion not found." }) : Ok(suggestion);
    }

    [HttpPost("suggestions/{id}/choose-template")]
    public async Task<ActionResult<FormRegistrySuggestion>> ChooseTemplate(
        string id,
        ChooseTemplateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CandidateTemplateFile)
            || string.IsNullOrWhiteSpace(request.CandidateDownloadPath))
        {
            return BadRequest(new { error = "CandidateTemplateFile and CandidateDownloadPath are required." });
        }

        var suggestion = await suggestionService.ChooseTemplateAsync(id, request, ct);
        return suggestion is null ? NotFound(new { error = "Suggestion not found." }) : Ok(suggestion);
    }
}
