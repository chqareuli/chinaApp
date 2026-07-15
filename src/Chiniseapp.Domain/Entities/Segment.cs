using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Entities;

/// <summary>
/// A single node in an entry's tree/node structured article. Every node —
/// including inline-element leaves (Xr/Style/Domain/Abbr/Lang) — is a Segment
/// row; parent-child links form the hierarchy described in the Editorial Panel
/// spec (entry → homonym → gramGrp → pos → sense → definition → example →
/// zh_segment/ka_segment).
/// </summary>
public class Segment
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    /// <summary>Null for a top-level child of the entry (a Homonym).</summary>
    public int? ParentSegmentId { get; set; }

    public Segment? Parent { get; set; }

    public ICollection<Segment> Children { get; } = new List<Segment>();

    public SegmentType SegmentType { get; set; }

    /// <summary>Sibling order under the same parent; system-assigned.</summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// System-generated display number for GramGrp (I, II, III...) or Sense
    /// (1, 2, 3...) segments. Read-only to the editor.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Plain leaf value for simple segments (e.g. a Pos code, ZhSegment text).
    /// Null for segments whose content is mixed (see <see cref="Content"/>) or
    /// purely structural (e.g. GramGrp, Sense containers).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// jsonb mixed-content array for Definition/KaSegment: an ordered sequence
    /// of text runs and references to inline-IE child segments, e.g.
    /// [{"type":"text","text":"..."},{"type":"segmentRef","segmentId":123},...].
    /// Null for segments that don't carry mixed content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// jsonb payload for inline-element segments (Xr/Style/Domain/Abbr/Lang),
    /// e.g. {"xrType":"seeAlso","label":"参见","target":"...","displayText":"..."}.
    /// Null for non-IE segments.
    /// </summary>
    public string? Attributes { get; set; }

    /// <summary>Only meaningful for Xr/Style/Domain/Abbr/Lang segments.</summary>
    public Placement? Placement { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
