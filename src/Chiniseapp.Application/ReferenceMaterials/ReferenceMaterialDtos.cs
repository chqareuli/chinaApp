namespace Chiniseapp.Application.ReferenceMaterials;

public record ReferenceMaterialDetail(
    int EntryId, string G1, string G2, string G3, string G4, string G5,
    string OriginalRawReferenceMaterial, DateTime UpdatedAtUtc);

/// <summary>
/// For material entered directly by an editor (not split from a legacy blob) —
/// OriginalRawReferenceMaterial stays empty for these rows. The M8 legacy-import
/// tool populates that field separately when parsing the old database.
/// </summary>
public record SaveReferenceMaterialRequest(string G1, string G2, string G3, string G4, string G5);
