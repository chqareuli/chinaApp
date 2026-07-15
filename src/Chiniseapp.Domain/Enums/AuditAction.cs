namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Semantic audit event kinds recorded explicitly by services. A separate
/// generic SaveChangesInterceptor (added when entities start changing in
/// practice) catches anything not covered by an explicit call.
/// </summary>
public enum AuditAction
{
    Created,
    StatusChanged,
    ContentEdited,
    RoleChanged,
    Deactivated,
    Archived,
    RepairModeToggled,
    Other,
}
