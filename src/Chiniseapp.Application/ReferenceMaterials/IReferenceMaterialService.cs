namespace Chiniseapp.Application.ReferenceMaterials;

/// <summary>
/// Auxiliary/legacy dictionary-prep material shown to the editor for reference only
/// (Editorial Panel spec §7) — never merges into definition/example content, never public.
/// </summary>
public interface IReferenceMaterialService
{
    Task<ReferenceMaterialDetail?> GetForEntryAsync(int entryId, CancellationToken ct = default);

    /// <summary>Creates or replaces the G1..G5 fields for an entry (1:1 relationship).</summary>
    Task<ReferenceMaterialDetail> SaveAsync(int entryId, SaveReferenceMaterialRequest request, CancellationToken ct = default);
}
