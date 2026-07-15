using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Application.Auth;

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    TokenResult CreateAccessToken(Editor editor);

    TokenResult CreateRefreshToken(Editor editor);

    /// <summary>
    /// Validates a refresh token's signature/expiry/type and returns the editor id
    /// it was issued for, or null if the token is invalid/expired/not a refresh token.
    /// </summary>
    int? ValidateRefreshToken(string refreshToken);
}
