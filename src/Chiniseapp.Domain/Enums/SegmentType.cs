namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Node kind within the entry tree: entry → homonym → gramGrp → pos → sense →
/// definition → example → zh_segment/ka_segment, plus inline-element leaf kinds
/// (Xr/Style/Domain/Abbr/Lang) that attach to a target segment or sit inline in
/// mixed content. Stage 1 only exercises Homonym/GramGrp(implicit)/Sense/
/// Definition/Example/ZhSegment/KaSegment; the rest exist so Stage 2 needs no
/// schema rewrite.
/// </summary>
public enum SegmentType
{
    Homonym,
    GramGrp,
    Pos,
    Sense,
    Definition,
    Example,
    ZhSegment,
    KaSegment,
    Xr,
    Style,
    Domain,
    Abbr,
    Lang,
}
