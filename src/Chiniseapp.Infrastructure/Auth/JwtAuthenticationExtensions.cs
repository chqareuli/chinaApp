using System.Text;
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
                    // Defense in depth: a blocked/deactivated editor's still-unexpired
                    // access token must stop working immediately, not just at its
                    // natural (short) expiry.
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (!int.TryParse(subject, out var editorId))
                        {
                            context.Fail("Missing or invalid subject claim.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<ChiniseDbContext>();
                        var isActive = await db.Editors
                            .Where(e => e.Id == editorId)
                            .Select(e => (bool?)e.IsActive)
                            .FirstOrDefaultAsync();

                        if (isActive != true)
                        {
                            context.Fail("Editor account is inactive or no longer exists.");
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
