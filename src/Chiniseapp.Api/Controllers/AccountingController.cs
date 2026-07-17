using System.Security.Claims;
using Chiniseapp.Application.Accounting;
using Chiniseapp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("accounting")]
[Authorize]
public class AccountingController(IAccountingService accountingService) : ControllerBase
{
    /// <summary>
    /// 10.2 global accounting page. Per the resolved Q5 decision, KA Editor and
    /// Assistant Editor cannot see everyone else's totals here — only their own,
    /// via <see cref="EditorDetail"/>.
    /// </summary>
    [HttpGet("editors")]
    [Authorize(Roles = $"{nameof(EditorRole.SuperAdmin)},{nameof(EditorRole.ChiefEditor)},{nameof(EditorRole.ZhEditor)}")]
    public async Task<ActionResult<IReadOnlyList<EditorScoreSummary>>> GlobalSummary(CancellationToken ct) =>
        Ok(await accountingService.GetGlobalSummaryAsync(ct));

    /// <summary>10.2 entry status totals — aggregate counts only, not personal data, open to any authenticated editor.</summary>
    [HttpGet("entries-status-totals")]
    public async Task<ActionResult<EntryStatusTotals>> EntryStatusTotals(CancellationToken ct) =>
        Ok(await accountingService.GetEntryStatusTotalsAsync(ct));

    /// <summary>
    /// 10.2 personal accounting page. Resolved Q5: KA Editor / Assistant Editor may only view
    /// their own; everyone else may view anyone's.
    /// </summary>
    [HttpGet("editors/{editorId:int}")]
    public async Task<ActionResult<EditorAccountingDetail>> EditorDetail(int editorId, CancellationToken ct)
    {
        var isSelf = editorId == User.GetEditorId();
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var isRestricted = role is nameof(EditorRole.KaEditor) or nameof(EditorRole.AssistantEditor);
        if (!isSelf && isRestricted)
        {
            return Problem(
                detail: "You are only allowed to view your own accounting page.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var detail = await accountingService.GetEditorDetailAsync(editorId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
