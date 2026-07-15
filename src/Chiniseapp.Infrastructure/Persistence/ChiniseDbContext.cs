using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the dictionary platform. Empty for now (M1) — entities
/// (Entry, Segment, Editor, Comment, ReferenceMaterial, ...) are added starting M2,
/// per the roadmap in the backend rebuild plan.
/// </summary>
public class ChiniseDbContext : DbContext
{
    public ChiniseDbContext(DbContextOptions<ChiniseDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
