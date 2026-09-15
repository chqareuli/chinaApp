using System.Text.Json;
using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

public record EditorListItem(int Id, string Email, string DisplayName, string Role, bool IsActive, DateTime CreatedAtUtc)
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? ChineseName { get; init; }
    public string? WeChatAccount { get; init; }
    public string? EditorialRole { get; init; }
    public bool IsSupervisor { get; init; }
    public bool IsCancelled { get; init; }
}

public record CreateEditorRequest(string Email, string DisplayName, string Role, string InitialPassword)
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? ChineseName { get; init; }
    public string? WeChatAccount { get; init; }
    public bool? IsSupervisor { get; init; }
}

public record ResetPasswordRequest(string NewPassword);

public record UpdateEditorRequest(
    string Email, string DisplayName, string Role, bool IsActive, string? NewPassword)
{
    public bool? IsSupervisor { get; init; }
    public bool? IsCancelled { get; init; }
}

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

    public record UpdateOwnProfileRequest(string DisplayName)
    {
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? ChineseName { get; init; }
        public string? Email { get; init; }
        public string? WeChatAccount { get; init; }
    }
    public record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateOwnProfileRequest request, CancellationToken ct)
    {
        var name = request.DisplayName.Trim();
        if (name.Length is 0 or > 200) return BadRequest(new { message = "Name must contain 1–200 characters." });
        var editor = await db.Editors.FindAsync([User.GetEditorId()], ct);
        if (editor is null) return NotFound();
        if (request.FirstName is not null || request.LastName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
                new[] {request.FirstName,request.LastName,request.ChineseName,request.WeChatAccount}.Any(v=>v?.Length>200))
                return BadRequest(new {message="Check the profile fields. · 请检查个人信息。"});
            var email = request.Email?.Trim();
            if (string.IsNullOrEmpty(email) || email.Length>255 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                return BadRequest(new {message="Enter a valid email. · 请输入有效邮箱。"});
            if(await db.Editors.AnyAsync(e=>e.Id!=editor.Id && e.Email.ToLower()==email.ToLower(),ct))
                return Conflict(new {message="Email already exists. · 邮箱已存在。"});
            name = $"{request.FirstName.Trim()} {request.LastName.Trim()}";
            if(name.Length>200) return BadRequest(new {message="Full name must be at most 200 characters. · 姓名最多200字。"});
            editor.FirstName=request.FirstName.Trim(); editor.LastName=request.LastName.Trim();
            editor.ChineseName=request.ChineseName?.Trim(); editor.WeChatAccount=request.WeChatAccount?.Trim(); editor.Email=email;
        }
        editor.DisplayName = name;
        await db.SaveChangesAsync(ct);
        return Ok(ToListItem(editor));
    }

    [HttpPost("me/password")]
    public async Task<IActionResult> ChangeOwnPassword(ChangeOwnPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length is < 8 or > 1024) return BadRequest(new { message = "Password must contain 8–1024 characters." });
        var editor = await db.Editors.FindAsync([User.GetEditorId()], ct);
        if (editor is null) return NotFound();
        if (!passwordHasher.VerifyPassword(editor, editor.PasswordHash, request.CurrentPassword))
            return BadRequest(new { message = "Current password is incorrect." });
        editor.PasswordHash = passwordHasher.HashPassword(editor, request.NewPassword);
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
        if (!Enum.TryParse<EditorRole>(request.Role, out var role) || !Enum.IsDefined(role))
        {
            return BadRequest(new { message = $"Unknown role '{request.Role}'." });
        }

        if (request.InitialPassword.Length is < 8 or > 1024 || string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { message = "Name and an 8–1024 character password are required. · 请填写姓名和8–1024位密码。" });
        if ((request.FirstName is not null || request.LastName is not null) &&
            (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)))
            return BadRequest(new { message = "First and last names are required. · 请填写名和姓。" });
        if (new[] { request.FirstName, request.LastName, request.ChineseName, request.WeChatAccount }.Any(v => v?.Length > 200))
            return BadRequest(new { message = "Each name or WeChat value must be at most 200 characters. · 每个字段最多200字。" });
        var editorialRole = role;
        if (request.IsSupervisor == true) role = EditorRole.SuperAdmin;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(41027)", ct);
        if (role == EditorRole.SuperAdmin && await db.Editors.CountAsync(e => e.Role == EditorRole.SuperAdmin && e.IsActive, ct) >= 2)
            return Conflict(new { message = "Maximum 2 active Supervisors. · 最多2位启用的主管。" });
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.Editors.AnyAsync(e => e.Email.ToLower() == normalizedEmail, ct))
        {
            return Conflict(new { message = "An editor with this email already exists." });
        }

        var editor = new Editor
        {
            Email = request.Email.Trim(),
            DisplayName = request.FirstName is not null ? $"{request.FirstName.Trim()} {request.LastName!.Trim()}" : request.DisplayName.Trim(),
            FirstName = request.FirstName?.Trim(), LastName = request.LastName?.Trim(),
            ChineseName = request.ChineseName?.Trim(), WeChatAccount = request.WeChatAccount?.Trim(),
            EditorialRole = editorialRole == EditorRole.SuperAdmin ? null : editorialRole.ToString(),
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        editor.PasswordHash = passwordHasher.HashPassword(editor, request.InitialPassword);

        db.Editors.Add(editor);
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = editor.Id }, ToListItem(editor));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<ActionResult<EditorListItem>> Update(
        int id, UpdateEditorRequest request, CancellationToken ct)
    {
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<EditorRole>(request.Role, out var role) || !Enum.IsDefined(role))
        {
            return BadRequest(new { message = $"Unknown role '{request.Role}'." });
        }

        var editorialRole = role;
        if (request.IsSupervisor == true) role = EditorRole.SuperAdmin;
        if (editorialRole == EditorRole.SuperAdmin && request.IsSupervisor == false)
            return BadRequest(new { message = "Select an editorial role before removing Supervisor. · 请先选择编辑角色。" });
        var active = request.IsActive && !(request.IsCancelled ?? editor.IsCancelled);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(41027)", ct);
        if (role == EditorRole.SuperAdmin && active && await db.Editors.CountAsync(e => e.Id != id && e.Role == EditorRole.SuperAdmin && e.IsActive, ct) >= 2)
            return Conflict(new { message = "Maximum 2 active Supervisors. · 最多2位启用的主管。" });
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest(new { message = "Name and email are required." });
        }

        if (request.NewPassword is { Length: > 0 and < 8 })
        {
            return BadRequest(new { message = "The new password must contain at least 8 characters." });
        }

        var normalizedEmail = email.ToLowerInvariant();
        if (await db.Editors.AnyAsync(e => e.Id != id && e.Email.ToLower() == normalizedEmail, ct))
        {
            return Conflict(new { message = "An editor with this email already exists." });
        }

        if (editor.Role == EditorRole.SuperAdmin && (role != EditorRole.SuperAdmin || !active))
        {
            var otherActiveSupervisors = await db.Editors.CountAsync(
                e => e.Id != id && e.Role == EditorRole.SuperAdmin && e.IsActive, ct);
            if (otherActiveSupervisors == 0)
            {
                return Conflict(new { message = "The last active supervisor cannot be demoted or deactivated." });
            }
        }

        var previousRole = editor.Role;
        editor.Email = email;
        editor.DisplayName = displayName;
        editor.Role = role;
        editor.IsActive = active;
        editor.IsCancelled = request.IsCancelled ?? editor.IsCancelled;
        if (editorialRole != EditorRole.SuperAdmin) editor.EditorialRole = editorialRole.ToString();
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            editor.PasswordHash = passwordHasher.HashPassword(editor, request.NewPassword);
        }

        if (previousRole != role)
        {
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                EntityType = nameof(Editor),
                EntityId = editor.Id,
                Action = AuditAction.RoleChanged,
                PerformedByEditorId = User.GetEditorId(),
                PerformedAtUtc = DateTime.UtcNow,
                OldValue = JsonSerializer.Serialize(new { role = previousRole.ToString() }),
                NewValue = JsonSerializer.Serialize(new { role = role.ToString() }),
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(ToListItem(editor));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(41027)", ct);
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        if (editor.Role == EditorRole.SuperAdmin && !await db.Editors.AnyAsync(e => e.Id != id && e.Role == EditorRole.SuperAdmin && e.IsActive, ct))
            return Conflict(new { message = "The last active supervisor cannot be deactivated." });
        editor.IsActive = false;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Roles = nameof(EditorRole.SuperAdmin))]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(41027)", ct);
        var editor = await db.Editors.FindAsync([id], ct);
        if (editor is null)
        {
            return NotFound();
        }

        if (editor.Role == EditorRole.SuperAdmin && await db.Editors.CountAsync(e => e.Id != id && e.Role == EditorRole.SuperAdmin && e.IsActive, ct) >= 2)
            return Conflict(new { message = "Maximum 2 active Supervisors. · 最多2位启用的主管。" });
        editor.IsActive = true;
        editor.IsCancelled = false;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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
        new(e.Id, e.Email, e.DisplayName, e.Role.ToString(), e.IsActive, e.CreatedAtUtc)
        { FirstName = e.FirstName, LastName = e.LastName, ChineseName = e.ChineseName, WeChatAccount = e.WeChatAccount,
          EditorialRole = e.EditorialRole, IsSupervisor = e.Role == EditorRole.SuperAdmin, IsCancelled = e.IsCancelled };
}
