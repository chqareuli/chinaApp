using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// One awarded score. The (EntryId, EditorId, ScoreType) unique index enforces
/// "once per entry" at the database level, matching the spec's scoring rules.
/// </summary>
public class ScoreEntry
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    public int EditorId { get; set; }

    public ScoreType ScoreType { get; set; }

    public DateTime AwardedAtUtc { get; set; }
}
