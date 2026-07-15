using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Chiniseapp.Infrastructure.Auth;

/// <summary>
/// PBKDF2 hashing via ASP.NET Core Identity's PasswordHasher&lt;T&gt; — used
/// standalone (no UserManager/IdentityDbContext) since Editor is a plain
/// entity, not IdentityUser&lt;int&gt;. Identity's PasswordHasher works with
/// any class; it doesn't require inheriting from IdentityUser.
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<Editor> _hasher = new();

    public string HashPassword(Editor editor, string password) => _hasher.HashPassword(editor, password);

    public bool VerifyPassword(Editor editor, string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(editor, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
