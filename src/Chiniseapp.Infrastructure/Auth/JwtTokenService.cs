using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chiniseapp.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    /// <summary>Distinguishes access from refresh tokens so one can't be used as the other.</summary>
    private const string TokenTypeClaim = "token_type";
    private const string AccessTokenType = "access";
    private const string RefreshTokenType = "refresh";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenResult CreateAccessToken(Editor editor)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = BaseClaims(editor).Append(new Claim(TokenTypeClaim, AccessTokenType));
        return new TokenResult(WriteToken(claims, expiresAtUtc), expiresAtUtc);
    }

    public TokenResult CreateRefreshToken(Editor editor)
    {
        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        // Refresh tokens intentionally carry only identity, not role/display name —
        // RefreshAsync always re-reads the current Editor row so a role change or
        // deactivation takes effect immediately instead of surviving in a stale claim.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, editor.Id.ToString()),
            new Claim(TokenTypeClaim, RefreshTokenType),
        };
        return new TokenResult(WriteToken(claims, expiresAtUtc), expiresAtUtc);
    }

    public int? ValidateRefreshToken(string refreshToken)
    {
        var principal = Validate(refreshToken);
        if (principal is null)
        {
            return null;
        }

        var tokenType = principal.FindFirstValue(TokenTypeClaim);
        if (tokenType != RefreshTokenType)
        {
            return null;
        }

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(subject, out var editorId) ? editorId : null;
    }

    private static IEnumerable<Claim> BaseClaims(Editor editor) =>
    [
        new Claim(ClaimTypes.NameIdentifier, editor.Id.ToString()),
        new Claim(ClaimTypes.Email, editor.Email),
        new Claim(ClaimTypes.Name, editor.DisplayName),
        new Claim(ClaimTypes.Role, editor.Role.ToString()),
    ];

    private string WriteToken(IEnumerable<Claim> claims, DateTime expiresAtUtc)
    {
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: _signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = JwtAuthenticationExtensions.BuildValidationParameters(_options);
        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
