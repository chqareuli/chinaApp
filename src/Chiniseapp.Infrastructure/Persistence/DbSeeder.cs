using Chiniseapp.Application.Auth;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chiniseapp.Infrastructure.Persistence;

/// <summary>
/// Bootstraps a single super_admin account from configuration on startup, idempotently
/// (no-op once any super_admin exists), so the system is usable without ever hand-writing
/// a password hash into a migration or source file.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedSuperAdminAsync(
        ChiniseDbContext db,
        IPasswordHasherService passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        var alreadySeeded = await db.Editors.AnyAsync(e => e.Role == EditorRole.SuperAdmin, ct);
        if (alreadySeeded)
        {
            return;
        }

        var email = configuration["Seed:SuperAdminEmail"];
        var password = configuration["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No super_admin exists and Seed:SuperAdminEmail/Seed:SuperAdminPassword are not " +
                "configured — skipping bootstrap. Set them via 'dotnet user-secrets' in Chiniseapp.Api.");
            return;
        }

        var editor = new Editor
        {
            Email = email.Trim(),
            DisplayName = "Super Admin",
            Role = EditorRole.SuperAdmin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        editor.PasswordHash = passwordHasher.HashPassword(editor, password);

        db.Editors.Add(editor);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Bootstrapped initial super_admin account for {Email}.", editor.Email);
    }
}
