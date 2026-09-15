using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

[ApiController, Authorize]
public class SupportMaterialsController(ChiniseDbContext db) : ControllerBase
{
    public record AddRequest(string Language, string CommentText);
    private bool CanDelete(SupportMaterial item) => item.AuthorEditorId == User.GetEditorId() || User.GetEditorRole() == EditorRole.SuperAdmin;
    private object Summary(SupportMaterial item) => new { item.Id, item.EntryId, item.Language, item.CommentText, item.CreatedAtUtc, CanDelete = CanDelete(item) };

    [HttpGet("entries/{entryId:int}/support-materials")]
    public async Task<IActionResult> List(int entryId, CancellationToken ct)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), entry.Status)) return NotFound();
        return Ok((await db.SupportMaterials.AsNoTracking().Where(m => m.EntryId == entryId).OrderByDescending(m => m.CreatedAtUtc).ToListAsync(ct)).Select(Summary));
    }
    [HttpPost("entries/{entryId:int}/support-materials")]
    public async Task<IActionResult> Add(int entryId, AddRequest request, CancellationToken ct)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null) return NotFound();
        if (!EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        if (request.Language is not ("Chinese" or "English" or "Russian" or "Other") || string.IsNullOrWhiteSpace(request.CommentText) || request.CommentText.Length > 100000)
            return BadRequest(new { message = "Choose a language and enter 1–100,000 characters." });
        var item = new SupportMaterial { EntryId = entryId, AuthorEditorId = User.GetEditorId(), Language = request.Language, CommentText = request.CommentText.Trim(), CreatedAtUtc = DateTime.UtcNow };
        db.SupportMaterials.Add(item);
        await db.SaveChangesAsync(ct);
        return Ok(Summary(item));
    }
    [HttpDelete("entries/{entryId:int}/support-materials/{id:int}")]
    public async Task<IActionResult> Delete(int entryId, int id, CancellationToken ct)
    {
        var item = await db.SupportMaterials.FindAsync([id], ct);
        if (item is null || item.EntryId != entryId) return NotFound();
        var entry = await db.Entries.FindAsync([item.EntryId], ct);
        if (entry is null || !CanDelete(item) || !EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        db.SupportMaterials.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
