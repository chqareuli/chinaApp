using Chiniseapp.Application.Entries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("vocabularies")]
[Authorize]
public class VocabulariesController(IEntryService entries) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VocabularyListItem>>> List(
        [FromQuery] string? category, CancellationToken ct)
    {
        var items = await entries.GetVocabulariesAsync(ct);
        return Ok(string.IsNullOrWhiteSpace(category) ? items : items.Where(v => v.Category == category).ToList());
    }
}
