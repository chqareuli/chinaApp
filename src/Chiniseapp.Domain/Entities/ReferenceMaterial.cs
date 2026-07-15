namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Auxiliary/legacy dictionary-prep material shown to the editor for reference
/// only; never auto-merges into definition/example content. 1:1 with
/// <see cref="Entry"/>. Legacy data is split into 5 segments (G1..G5) by
/// literal asterisk-run delimiters (**, ***, ****, *****); empty segments stay
/// empty strings, never null.
/// </summary>
public class ReferenceMaterial
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    /// <summary>English-Chinese material (rendered maroon; Chinese chars stay black).</summary>
    public string G1 { get; set; } = string.Empty;

    /// <summary>Chinese explanatory material (rendered black).</summary>
    public string G2 { get; set; } = string.Empty;

    /// <summary>Chinese-Russian material (rendered blue; Chinese chars stay black).</summary>
    public string G3 { get; set; } = string.Empty;

    /// <summary>Additional material (rendered green, no background).</summary>
    public string G4 { get; set; } = string.Empty;

    /// <summary>Additional information (rendered purple, no background).</summary>
    public string G5 { get; set; } = string.Empty;

    /// <summary>Untouched raw legacy blob this row was split from, kept for migration safety.</summary>
    public string OriginalRawReferenceMaterial { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
