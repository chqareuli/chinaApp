namespace Chiniseapp.Domain.Enums;

/// <summary>
/// How an inline element (XR/STYLE/DOMAIN/Abbr/Lang) relates to its target segment:
/// Inline sits inside mixed-content text (Definition/KaSegment "Content" array),
/// Attached decorates a whole segment (e.g. STYLE/DOMAIN on a Sense), Standalone
/// is a dedicated cross-reference attached to a whole segment (e.g. a standalone XR).
/// </summary>
public enum Placement
{
    Inline,
    Attached,
    Standalone,
}
