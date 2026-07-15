using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Generic audit record; also doubles as status history (AuditAction.StatusChanged
/// entries carry the old/new status in OldValue/NewValue). EntityType/EntityId
/// identify the affected row generically (e.g. "Entry"/entryId) rather than via
/// a typed FK, since audit rows must outlive the entities they describe.
/// </summary>
public class AuditLogEntry
{
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public AuditAction Action { get; set; }

    public int PerformedByEditorId { get; set; }

    public DateTime PerformedAtUtc { get; set; }

    /// <summary>jsonb snapshot of the changed value(s) before the action, if applicable.</summary>
    public string? OldValue { get; set; }

    /// <summary>jsonb snapshot of the changed value(s) after the action, if applicable.</summary>
    public string? NewValue { get; set; }

    public string? Notes { get; set; }
}
