using System.Text;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Chiniseapp.Infrastructure.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtOptions.SectionName}' was not found. " +
                "Set Jwt:SigningKey/Issuer/Audience via 'dotnet user-secrets' in Chiniseapp.Api.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearerOptions =>
            {
                bearerOptions.TokenValidationParameters = BuildValidationParameters(jwtOptions);
                bearerOptions.Events = new JwtBearerEvents
                {
                    // Defense in depth: deactivation and role changes invalidate an
                    // otherwise-unexpired access token immediately. The client can then
                    // refresh and receive a token with the current role.
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (!int.TryParse(subject, out var editorId))
                        {
                            context.Fail("Missing or invalid subject claim.");
                            return;
                        }

                        var roleClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                        if (!Enum.TryParse<EditorRole>(roleClaim, out var claimedRole) || !Enum.IsDefined(claimedRole))
                        {
                            context.Fail("Missing or invalid role claim.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<ChiniseDbContext>();
                        var editorState = await db.Editors
                            .Where(e => e.Id == editorId)
                            .Select(e => new { e.IsActive, e.Role })
                            .FirstOrDefaultAsync();

                        if (editorState is null || !editorState.IsActive)
                        {
                            context.Fail("Editor account is inactive or no longer exists.");
                            return;
                        }

                        if (editorState.Role != claimedRole)
                        {
                            context.Fail("Editor role has changed; refresh the access token.");
                        }
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static TokenValidationParameters BuildValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
