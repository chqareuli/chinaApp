using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Auth;

public class AuthService(
    ChiniseDbContext db,
    IPasswordHasherService passwordHasher,
    IJwtTokenService tokenService) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var editor = await db.Editors.SingleOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail, ct);

        if (editor is null || !passwordHasher.VerifyPassword(editor, editor.PasswordHash, password))
        {
            throw new AuthException(AuthFailureReason.InvalidCredentials);
        }

        if (!editor.IsActive)
        {
            throw new AuthException(AuthFailureReason.AccountInactive);
        }

        editor.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return IssueTokens(editor);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var editorId = tokenService.ValidateRefreshToken(refreshToken);
        if (editorId is null)
        {
            throw new AuthException(AuthFailureReason.InvalidOrExpiredToken);
        }

        // Always re-read the current row (not claims from the refresh token
        // itself) so a role change or deactivation since the token was issued
        // takes effect immediately.
        var editor = await db.Editors.FindAsync([editorId.Value], ct);
        if (editor is null)
        {
            throw new AuthException(AuthFailureReason.InvalidOrExpiredToken);
        }

        if (!editor.IsActive)
        {
            throw new AuthException(AuthFailureReason.AccountInactive);
        }

        return IssueTokens(editor);
    }

    private AuthResult IssueTokens(Editor editor)
    {
        var access = tokenService.CreateAccessToken(editor);
        var refresh = tokenService.CreateRefreshToken(editor);
        var summary = new EditorSummary(editor.Id, editor.Email, editor.DisplayName, editor.Role.ToString());
        return new AuthResult(access.Token, access.ExpiresAtUtc, refresh.Token, refresh.ExpiresAtUtc, summary);
    }
}
