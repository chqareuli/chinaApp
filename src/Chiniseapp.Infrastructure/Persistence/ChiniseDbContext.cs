using Chiniseapp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Chiniseapp.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the dictionary platform. Reduced Stage-1 entity set per
/// the backend rebuild plan (docs/backend-plan.md) — the full SegmentType
/// vocabulary already exists so Stage 2 needs no schema rewrite, but only the
/// Entry/Segment/Editor/etc. shape described there is wired up so far.
/// </summary>
public class ChiniseDbContext : DbContext
{
    /// <summary>
    /// Shared translator so enum label generation is identical between the
    /// CREATE TYPE labels registered here and the runtime enum mapping
    /// registered on the NpgsqlDataSource in DependencyInjection.cs — both
    /// must agree, or inserts/reads of enum columns fail at runtime.
    /// </summary>
    public static readonly NpgsqlSnakeCaseNameTranslator EnumNameTranslator = new();

    public ChiniseDbContext(DbContextOptions<ChiniseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<ControlledVocabulary> ControlledVocabularies => Set<ControlledVocabulary>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();
    public DbSet<Editor> Editors => Set<Editor>();
    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<Domain.Enums.EntryStatus>(name: "entry_status", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.EditorRole>(name: "editor_role", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.SegmentType>(name: "segment_type", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.Placement>(name: "placement", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.ScoreType>(name: "score_type", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.CommentStatus>(name: "comment_status", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.AuditAction>(name: "audit_action", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.VocabularyCategory>(name: "vocabulary_category", nameTranslator: EnumNameTranslator);
        modelBuilder.HasPostgresEnum<Domain.Enums.MediaFileType>(name: "media_file_type", nameTranslator: EnumNameTranslator);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChiniseDbContext).Assembly);
    }
}
