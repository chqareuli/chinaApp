using System.Security.Claims;
using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

public record EditorListItem(int Id, string Email, string DisplayName, string Role, bool IsActive, DateTime CreatedAtUtc);

public record CreateEditorRequest(string Email, string DisplayName, string Role, string InitialPassword);

public record ResetPasswordRequest(string NewPassword);

/// <summary>
/// Bootstrap-level editor management. Only what M3 (auth) needs to be usable —
/// full profile self-edit, richer filtering, etc. can grow later without
/// breaking this shape.
/// </summary>
[ApiController]
[Route("editors")]
[Authorize]
public class EditorsController(ChiniseDbContext db, IPasswordHasherService passwordHasher) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<EditorListItem>> Me(CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([CurrentEditorId()], ct);
        if (editor is null)
        {
            return NotFound();
        }

        return Ok(ToListItem(editor));
    }

    [HttpGet]
    public async Task<ActionResult<List<EditorListItem>>> List(CancellationToken ct)
    {
        var editors = await db.Editors.OrderBy(e => e.DisplayName).ToListAsync(ct);
        return Ok(editors.Select(ToListItem));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EditorListItem>> Get(int id, CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([id], ct);
        return editor is null ? NotFound() : Ok(ToListItem(editor));
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(EditorRole.SuperAdmin)},{nameof(EditorRole.ChiefEditor)}")]
    public async Task<ActionResult<EditorListItem>> Create(CreateEditorRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<EditorRole>(request.Role, out var role))
        {
            return BadRequest(new { message = $"Unknown role '{request.Role}'." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.Editors.AnyAsync(e => e.Email.ToLower() == normalizedEmail, ct))
        {
            return Conflict(new { message = "An editor with this email already exists." });
        }

        var editor = new Editor
        {
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        editor.PasswordHash = passwordHasher.HashPassword(editor, request.InitialPassword);

        db.Editors.Add(editor);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = editor.Id }, ToListItem(editor));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        editor.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        editor.IsActive = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request, CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        // Super Admin can reset/replace a password, never view the existing one
        // (there is nothing to view — only a PBKDF2 hash is ever stored).
        editor.PasswordHash = passwordHasher.HashPassword(editor, request.NewPassword);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private int CurrentEditorId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static EditorListItem ToListItem(Editor e) =>
        new(e.Id, e.Email, e.DisplayName, e.Role.ToString(), e.IsActive, e.CreatedAtUtc);
}
