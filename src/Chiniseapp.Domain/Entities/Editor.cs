using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// A dictionary editor/admin account. Deliberately a plain entity rather than
/// deriving from ASP.NET Identity's IdentityUser&lt;T&gt; — Domain stays
/// dependency-free, and Role is an exclusive typed enum, not Identity's
/// multi-role system. JWT issuance and PasswordHasher wiring arrive in M3
/// (Infrastructure/Api), using this entity as the user store's model.
/// </summary>
public class Editor
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash only — never a viewable/reversible password, per spec.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public EditorRole Role { get; set; }

    /// <summary>Block/deactivate instead of hard-delete, per spec.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Historical-only metadata for editors migrated from the retired
    /// "legacy auxiliary preparer" role. Not a live role in the new workflow.
    /// </summary>
    public string? LegacyRoleRaw { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
}
