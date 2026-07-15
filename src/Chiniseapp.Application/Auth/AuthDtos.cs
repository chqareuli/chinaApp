namespace Chiniseapp.Application.Auth;

public record EditorSummary(int Id, string Email, string DisplayName, string Role);

public record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    EditorSummary Editor);

/// <summary>Reason a login/refresh attempt failed, so the Api layer can pick the right HTTP status.</summary>
public enum AuthFailureReason
{
    InvalidCredentials,
    AccountInactive,
    InvalidOrExpiredToken,
}

public class AuthException(AuthFailureReason reason) : Exception
{
    public AuthFailureReason Reason { get; } = reason;
}
