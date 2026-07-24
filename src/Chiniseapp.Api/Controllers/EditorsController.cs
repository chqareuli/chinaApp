using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

public record EditorListItem(int Id, string Email, string DisplayName, string Role, bool IsActive, DateTime CreatedAtUtc);

public record CreateEditorRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string DisplayName,
    [property: Required] string Role,
    [property: Required, MinLength(8)] string InitialPassword);

public record ResetPasswordRequest(string NewPassword);

public record ChangeEditorRoleRequest(string Role);

public record UpdateOwnProfileRequest(string DisplayName);

public record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);

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
        var editor = await db.Editors.FindAsync([User.GetEditorId()], ct);
        if (editor is null)
        {
            return NotFound();
        }

        return Ok(ToListItem(editor));
    }

    [HttpPut("me")]
    public async Task<ActionResult<EditorListItem>> UpdateMe(UpdateOwnProfileRequest request, CancellationToken ct)
    {
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 160)
        {
            return BadRequest(new { message = "Display name is required and must be 160 characters or fewer." });
        }

        var editor = await db.Editors.FindAsync([User.GetEditorId()], ct);
        if (editor is null)
        {
            return NotFound();
        }

        editor.DisplayName = displayName;
        await db.SaveChangesAsync(ct);
        return Ok(ToListItem(editor));
    }

    [HttpPost("me/password")]
    public async Task<IActionResult> ChangeOwnPassword(ChangeOwnPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "The new password must be at least 8 characters long." });
        }

        var editor = await db.Editors.FindAsync([User.GetEditorId()], ct);
        if (editor is null)
        {
            return NotFound();
        }

        if (!passwordHasher.VerifyPassword(editor, editor.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new { message = "The current password is incorrect." });
        }

        editor.PasswordHash = passwordHasher.HashPassword(editor, request.NewPassword);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Editor),
            EntityId = editor.Id,
            Action = AuditAction.Other,
            PerformedByEditorId = editor.Id,
            PerformedAtUtc = DateTime.UtcNow,
            Notes = "Password changed by editor.",
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
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
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<ActionResult<EditorListItem>> Create(CreateEditorRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Display name is required." });
        }

        if (!Enum.TryParse<EditorRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
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

    [HttpPut("{id:int}/role")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<ActionResult<EditorListItem>> ChangeRole(
        int id,
        ChangeEditorRoleRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<EditorRole>(request.Role, ignoreCase: true, out var newRole) || !Enum.IsDefined(newRole))
        {
            return BadRequest(new { message = $"Unknown role '{request.Role}'." });
        }

        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        if (editor.Role == newRole)
        {
            return BadRequest(new { message = "The editor already has that role." });
        }

        if (editor.Role == EditorRole.SuperAdmin &&
            newRole != EditorRole.SuperAdmin &&
            !await HasAnotherActiveSuperAdminAsync(editor.Id, ct))
        {
            return Conflict(new { message = "The last active super admin cannot be demoted." });
        }

        var previousRole = editor.Role;
        editor.Role = newRole;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Editor),
            EntityId = editor.Id,
            Action = AuditAction.RoleChanged,
            PerformedByEditorId = User.GetEditorId(),
            PerformedAtUtc = DateTime.UtcNow,
            OldValue = JsonSerializer.Serialize(new { role = previousRole.ToString() }),
            NewValue = JsonSerializer.Serialize(new { role = newRole.ToString() }),
        });

        await db.SaveChangesAsync(ct);
        return Ok(ToListItem(editor));
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

        if (editor.Id == User.GetEditorId())
        {
            return BadRequest(new { message = "You cannot deactivate your own account." });
        }

        if (editor.Role == EditorRole.SuperAdmin &&
            editor.IsActive &&
            !await HasAnotherActiveSuperAdminAsync(editor.Id, ct))
        {
            return Conflict(new { message = "The last active super admin cannot be deactivated." });
        }

        editor.IsActive = false;
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Editor),
            EntityId = editor.Id,
            Action = AuditAction.Deactivated,
            PerformedByEditorId = User.GetEditorId(),
            PerformedAtUtc = DateTime.UtcNow,
        });
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

    private static EditorListItem ToListItem(Editor e) =>
        new(e.Id, e.Email, e.DisplayName, e.Role.ToString(), e.IsActive, e.CreatedAtUtc);

    private async Task<bool> HasAnotherActiveSuperAdminAsync(int editorId, CancellationToken ct) =>
        await db.Editors.AnyAsync(
            e => e.Id != editorId && e.IsActive && e.Role == EditorRole.SuperAdmin,
            ct);
}
