using Chiniseapp.Application.ReferenceMaterials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Chiniseapp.Infrastructure.Persistence;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.Scoring;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chiniseapp.Api.Controllers;

/// <summary>
/// Auxiliary/legacy dictionary-prep material shown to the editor for reference only —
/// never part of the public entry content.
/// </summary>
[ApiController]
[Route("entries/{entryId:int}/reference-material")]
[Authorize]
public class ReferenceMaterialController(IReferenceMaterialService referenceMaterialService, ChiniseDbContext db, INotificationService notifications, IScoringService scores) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReferenceMaterialDetail>> Get(int entryId, CancellationToken ct)
    {
        var entry = await db.Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), entry.Status)) return NotFound();
        var detail = await referenceMaterialService.GetForEntryAsync(entryId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPut]
    [Authorize(Roles = AuthorizationPolicies.ContentEditorRoles)]
    public async Task<ActionResult<ReferenceMaterialDetail>> Save(int entryId, SaveReferenceMaterialRequest request, CancellationToken ct)
    {
        try
        {
            var entry = await db.Entries.SingleOrDefaultAsync(e => e.Id == entryId, ct);
            if (entry is null) return NotFound();
            if (!EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
            var before = await referenceMaterialService.GetForEntryAsync(entryId, ct);
            var segments = await db.Segments.AsNoTracking().Where(s => s.EntryId == entryId).ToListAsync(ct);
            var content = EntryTreeMapper.Read(segments);
            var forms = EntryTreeMapper.ReadForms(entry);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var detail = await referenceMaterialService.SaveAsync(entryId, request, ct);
            db.AuditLogEntries.Add(new AuditLogEntry { EntityType = nameof(Entry), EntityId = entryId,
                PerformedByEditorId = User.GetEditorId(), PerformedAtUtc = DateTime.UtcNow, Action = AuditAction.ContentEdited,
                Notes = "Reference materials updated · 参考资料已更新",
                OldValue = JsonSerializer.Serialize(new { schemaVersion = 1, entry.Id, entry.Lemma, entry.Status, forms, content, reference = before }),
                NewValue = JsonSerializer.Serialize(new { schemaVersion = 1, entry.Id, entry.Lemma, entry.Status, forms, content, reference = detail }) });
            await scores.AwardForContentEditAsync(entry, User.GetEditorRole(), User.GetEditorId(), ct);
            await notifications.NotifyEntryChangedAsync(entry, User.GetEditorId(), nameof(AuditAction.ContentEdited), ct, "Reference materials updated · 参考资料已更新");
            entry.LastEditorEditorId = User.GetEditorId(); entry.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(detail);
        }
        catch (ReferenceMaterialValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
