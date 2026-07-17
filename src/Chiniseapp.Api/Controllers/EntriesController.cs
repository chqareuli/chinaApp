using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

/// <summary>Editors allowed to create/edit new_entry content, per the 11.1 permissions matrix.</summary>
file static class EntryEditorRoles
{
    public const string Names = $"{nameof(EditorRole.SuperAdmin)},{nameof(EditorRole.ChiefEditor)},{nameof(EditorRole.ZhEditor)},{nameof(EditorRole.AssistantEditor)}";
}

[ApiController]
[Route("entries")]
[Authorize]
public class EntriesController(IEntryService entryService) : ControllerBase
{
    /// <summary>5.1 Main editor page list: all entries except new_entry.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EntrySummary>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        return Ok(await entryService.GetMainListAsync(page, pageSize, ct));
    }

    /// <summary>5.2 Search dropdown: starts-with match, includes new_entry.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<EntrySummary>>> Search(
        [FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<EntrySummary>());
        }

        return Ok(await entryService.SearchAsync(q, limit, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EntryDetail>> Get(int id, CancellationToken ct)
    {
        var entry = await entryService.GetAsync(id, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>Minimum viable create: headword only (spec 9 — title is the only required field).</summary>
    [HttpPost]
    [Authorize(Roles = EntryEditorRoles.Names)]
    public async Task<ActionResult<EntryDetail>> Create(CreateEntryRequest request, CancellationToken ct)
    {
        try
        {
            var entry = await entryService.CreateAsync(request, User.GetEditorId(), ct);
            return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
        }
        catch (EntryValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Full-replace structured content save. All Stage-1 entries are new_entry
    /// (M5 adds the status-gated variants of this rule), so this is exactly
    /// "Edit new_entry content" from the 11.1 permissions matrix.
    /// </summary>
    [HttpPut("{id:int}/content")]
    [Authorize(Roles = EntryEditorRoles.Names)]
    public async Task<ActionResult<EntryDetail>> SaveContent(int id, SaveEntryContentRequest request, CancellationToken ct)
    {
        try
        {
            var entry = await entryService.SaveContentAsync(id, request, User.GetEditorId(), ct);
            return Ok(entry);
        }
        catch (EntryConcurrencyException)
        {
            return Conflict(new { message = "This entry was modified by someone else since you loaded it. Reload and try again." });
        }
        catch (EntryValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
