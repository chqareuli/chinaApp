using Chiniseapp.Application.Accounting;
using Chiniseapp.Application.Auth;
using Chiniseapp.Application.Comments;
using Chiniseapp.Application.Entries;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.ReferenceMaterials;
using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Accounting;
using Chiniseapp.Infrastructure.Auth;
using Chiniseapp.Infrastructure.Comments;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Infrastructure.Notifications;
using Chiniseapp.Infrastructure.Persistence;
using Chiniseapp.Infrastructure.ReferenceMaterials;
using Chiniseapp.Infrastructure.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Chiniseapp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. " +
                "Set it via 'dotnet user-secrets set ConnectionStrings:DefaultConnection \"...\"' in Chiniseapp.Api.");
        }

        // Native Postgres enum types must be mapped on the data source *before*
        // it's used, with the same name + translator as the CREATE TYPE labels
        // registered in ChiniseDbContext.OnModelCreating, or enum reads/writes
        // fail at runtime.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<EntryStatus>("entry_status", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<EditorRole>("editor_role", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<SegmentType>("segment_type", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<Placement>("placement", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<ScoreType>("score_type", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<CommentStatus>("comment_status", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<AuditAction>("audit_action", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<VocabularyCategory>("vocabulary_category", ChiniseDbContext.EnumNameTranslator);
        dataSourceBuilder.MapEnum<MediaFileType>("media_file_type", ChiniseDbContext.EnumNameTranslator);
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<ChiniseDbContext>(options =>
            options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention());

        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEntryService, EntryService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IReferenceMaterialService, ReferenceMaterialService>();

        return services;
    }
}
