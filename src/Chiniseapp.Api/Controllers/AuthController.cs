using Chiniseapp.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

public record LoginRequest(string Email, string Password);

public record AccessTokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, EditorSummary Editor);

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await authService.LoginAsync(request.Email, request.Password, ct);
            SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
            return Ok(new AccessTokenResponse(result.AccessToken, result.AccessTokenExpiresAtUtc, result.Editor));
        }
        catch (AuthException ex)
        {
            return Problem(ex.Reason);
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "No refresh token cookie present." });
        }

        try
        {
            var result = await authService.RefreshAsync(refreshToken, ct);
            SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
            return Ok(new AccessTokenResponse(result.AccessToken, result.AccessTokenExpiresAtUtc, result.Editor));
        }
        catch (AuthException ex)
        {
            return Problem(ex.Reason);
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAtUtc)
    {
        // SameSite=Lax/Strict is fine for a same-site Angular deployment; a
        // cross-origin Angular dev server will need SameSite=None + CORS
        // configuration, which is out of scope until the frontend exists.
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc,
            Path = "/auth",
        });
    }

    private ObjectResult Problem(AuthFailureReason reason)
    {
        var (status, detail) = reason switch
        {
            AuthFailureReason.InvalidCredentials => (StatusCodes.Status401Unauthorized, "Invalid email or password."),
            AuthFailureReason.AccountInactive => (StatusCodes.Status403Forbidden, "This account has been deactivated."),
            AuthFailureReason.InvalidOrExpiredToken => (StatusCodes.Status401Unauthorized, "Refresh token is invalid or expired."),
            _ => (StatusCodes.Status401Unauthorized, "Authentication failed."),
        };
        return Problem(detail: detail, statusCode: status);
    }
}
