using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

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

    /// <summary>Complete administrative list, including new_entry, with filters and sorting.</summary>
    [HttpGet("all")]
    public async Task<ActionResult<PagedResult<EntrySummary>>> All(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int? editorId = null,
        [FromQuery] string sort = nameof(EntryListSort.UpdatedDesc),
        CancellationToken ct = default)
    {
        EntryStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<EntryStatus>(status, ignoreCase: true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                return EntryProblem(
                    StatusCodes.Status400BadRequest,
                    "entry_status_invalid",
                    $"Unknown status '{status}'.");
            }

            statusFilter = parsedStatus;
        }

        if (editorId is <= 0)
        {
            return EntryProblem(
                StatusCodes.Status400BadRequest,
                "editor_filter_invalid",
                "Editor id must be a positive number.");
        }

        if (!Enum.TryParse<EntryListSort>(sort, ignoreCase: true, out var listSort) ||
            !Enum.IsDefined(listSort))
        {
            return EntryProblem(
                StatusCodes.Status400BadRequest,
                "entry_sort_invalid",
                $"Unknown sort '{sort}'.");
        }

        return Ok(await entryService.GetAllListAsync(
            page,
            pageSize,
            q,
            statusFilter,
            editorId,
            listSort,
            ct));
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
        var entry = await entryService.GetAsync(id, User.GetEditorId(), ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<IReadOnlyList<EntryHistoryItem>>> History(int id, CancellationToken ct)
    {
        var entry = await entryService.GetAsync(id, User.GetEditorId(), ct);
        if (entry is null)
        {
            return EntryProblem(StatusCodes.Status404NotFound, "entry_not_found", $"Entry {id} does not exist.");
        }

        return Ok(await entryService.GetHistoryAsync(id, ct));
    }

    /// <summary>Minimum viable create: headword only (spec 9 — title is the only required field).</summary>
    [HttpPost]
    [Authorize(Roles = AuthorizationPolicies.EntryCreatorRoles)]
    public async Task<ActionResult<EntryDetail>> Create(CreateEntryRequest request, CancellationToken ct)
    {
        try
        {
            var entry = await entryService.CreateAsync(request, User.GetEditorId(), ct);
            return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
        }
        catch (EntryValidationException ex)
        {
            return EntryProblem(StatusCodes.Status400BadRequest, "entry_validation", ex.Message);
        }
        catch (EntryContentForbiddenException ex)
        {
            return EntryProblem(StatusCodes.Status403Forbidden, "entry_content_forbidden", ex.Message);
        }
    }

    /// <summary>
    /// Full-replace structured content save. Authorization is status-aware and enforced in the
    /// service through Domain.Rules.EntryContentRules.
    /// </summary>
    [HttpPut("{id:int}/content")]
    public async Task<ActionResult<EntryDetail>> SaveContent(int id, SaveEntryContentRequest request, CancellationToken ct)
    {
        try
        {
            var entry = await entryService.SaveContentAsync(id, request, User.GetEditorId(), ct);
            return Ok(entry);
        }
        catch (EntryConcurrencyException)
        {
            return EntryProblem(
                StatusCodes.Status409Conflict,
                "entry_concurrency_conflict",
                "This entry was modified by someone else since you loaded it. Reload and try again.");
        }
        catch (EntryNotFoundException ex)
        {
            return EntryProblem(StatusCodes.Status404NotFound, "entry_not_found", ex.Message);
        }
        catch (EntryContentForbiddenException ex)
        {
            return EntryProblem(StatusCodes.Status403Forbidden, "entry_content_forbidden", ex.Message);
        }
        catch (EntryValidationException ex)
        {
            return EntryProblem(StatusCodes.Status400BadRequest, "entry_validation", ex.Message);
        }
    }

    public record ChangeStatusRequest(string TargetStatus);

    /// <summary>
    /// Status transition, gated by the resolved role x status table
    /// (Domain.Rules.StatusTransitionRules) rather than a static role list —
    /// permission genuinely depends on the entry's current status.
    /// </summary>
    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<EntryDetail>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<EntryStatus>(request.TargetStatus, ignoreCase: true, out var targetStatus) ||
            !Enum.IsDefined(targetStatus))
        {
            return EntryProblem(
                StatusCodes.Status400BadRequest,
                "entry_status_invalid",
                $"Unknown status '{request.TargetStatus}'.");
        }

        try
        {
            var entry = await entryService.ChangeStatusAsync(id, targetStatus, User.GetEditorId(), ct);
            return Ok(entry);
        }
        catch (EntryStatusTransitionForbiddenException ex)
        {
            return EntryProblem(StatusCodes.Status403Forbidden, "entry_status_transition_forbidden", ex.Message);
        }
        catch (EntryPublicationValidationException ex)
        {
            var result = EntryProblem(StatusCodes.Status400BadRequest, "entry_publication_invalid", ex.Message);
            ((ProblemDetails)result.Value!).Extensions["errors"] = ex.Errors;
            return result;
        }
        catch (EntryNotFoundException ex)
        {
            return EntryProblem(StatusCodes.Status404NotFound, "entry_not_found", ex.Message);
        }
        catch (EntryValidationException ex)
        {
            return EntryProblem(StatusCodes.Status400BadRequest, "entry_validation", ex.Message);
        }
    }

    private ObjectResult EntryProblem(int status, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
