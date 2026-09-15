using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Authorize]
public class EditorialActivityController(ChiniseDbContext db) : ControllerBase
{
    [HttpGet("activity/unread-counts")]
    public async Task<IActionResult> Unread(CancellationToken ct)
    {
        var editorId = User.GetEditorId();
        return Ok(new {
            notifications = await db.Notifications.CountAsync(n => n.RecipientEditorId == editorId && !n.IsRead, ct),
            messages = await db.DirectMessages.CountAsync(m => m.RecipientEditorId == editorId && m.ReadAtUtc == null, ct)
        });
    }

    [HttpGet("entries/{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken ct)
    {
        var entry = await db.Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), entry.Status)) return NotFound();
        return Ok(await (from a in db.AuditLogEntries.AsNoTracking()
            join editor in db.Editors on a.PerformedByEditorId equals editor.Id
            where a.EntityType == nameof(Entry) && a.EntityId == id
            orderby a.PerformedAtUtc descending, a.Id descending
            select new { a.Id, Action = a.Action.ToString(), a.PerformedByEditorId,
                PerformedByDisplayName = editor.DisplayName, a.PerformedAtUtc, a.OldValue, a.NewValue, a.Notes }).ToListAsync(ct));
    }
}
