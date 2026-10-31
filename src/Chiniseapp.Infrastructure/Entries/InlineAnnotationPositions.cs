using Chiniseapp.Application.Entries;

namespace Chiniseapp.Infrastructure.Entries;

/// <summary>Offsets count zero-based UTF-16 code units in the exact saved field.</summary>
public static class InlineAnnotationPositions
{
    public static (List<MarkerDto> Markers, List<CrossReferenceDto> References) Resolve(
        int? parentId, IReadOnlyDictionary<string, string> fields, List<MarkerDto> markers, List<CrossReferenceDto> references)
    {
        int Offset(string? field, int? offset, string display)
        {
            if (field is null || !fields.TryGetValue(field, out var text) || display.Length == 0)
                throw new EntryValidationException($"Segment {parentId}: inline annotation requires a supported text field and display text.");
            if (offset is >= 0 && offset <= text.Length - display.Length && text.AsSpan(offset.Value, display.Length).SequenceEqual(display.AsSpan()))
                return offset.Value;
            var found = text.IndexOf(display, StringComparison.Ordinal);
            if (found >= 0 && text.IndexOf(display, found + 1, StringComparison.Ordinal) < 0) return found;
            throw new EntryValidationException($"Segment {parentId}, field {field}: inline annotation position is missing or ambiguous. Reinsert the annotation.");
        }
        return (
            markers.Select(m => m.TextField is null ? m : m with { TextOffset = Offset(m.TextField, m.TextOffset, m.DisplayText ?? m.Value) }).ToList(),
            references.Select(r => r.Placement != "Inline" ? r : r with { TextOffset = Offset(r.TextField, r.TextOffset, r.DisplayText ?? r.Label) }).ToList());
    }
}
