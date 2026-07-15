using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Application.Auth;

/// <summary>
/// Thin wrapper around a PBKDF2 password hasher. The spec requires passwords be
/// stored hashed-only and never viewable/reversible by anyone, including
/// Super Admin — this is the only place that touches raw password bytes.
/// </summary>
public interface IPasswordHasherService
{
    string HashPassword(Editor editor, string password);

    bool VerifyPassword(Editor editor, string hashedPassword, string providedPassword);
}
