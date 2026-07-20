using Chiniseapp.Application.PublicDictionary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("public/dictionary")]
[AllowAnonymous]
public class PublicDictionaryController(IPublicDictionaryService dictionary) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PublicDictionarySearchItem>>> Search(
        [FromQuery] string q,
        [FromQuery] string mode = "word",
        [FromQuery] int limit = 25,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<PublicSearchMode>(mode, ignoreCase: true, out var searchMode))
        {
            return BadRequest(new { message = "Search mode must be 'word' or 'text'." });
        }

        return Ok(await dictionary.SearchAsync(q ?? string.Empty, searchMode, limit, ct));
    }

    [HttpGet("entries/{id:int}")]
    public async Task<ActionResult<PublicDictionaryEntry>> Entry(int id, CancellationToken ct)
    {
        var entry = await dictionary.GetEntryAsync(id, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<PublicDictionaryStats>> Stats(CancellationToken ct) =>
        Ok(await dictionary.GetStatsAsync(ct));
}
