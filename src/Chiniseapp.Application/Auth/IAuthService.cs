namespace Chiniseapp.Application.Auth;

public interface IAuthService
{
    /// <summary>Throws <see cref="AuthException"/> on invalid credentials or an inactive account.</summary>
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Throws <see cref="AuthException"/> if the refresh token is invalid/expired or the account is inactive.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
}
