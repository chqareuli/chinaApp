using System.Text.Json;
using System.Xml.Linq;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Infrastructure.Entries;

/// <summary>Dictionary exports do not expose the editor's legacy summary lists.</summary>
public static class EntryExportSerializer
{
    public const int SchemaVersion = 2;
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";
    public static object DictionaryContent(EntryDetail e) => DictionaryContent(e, []);
    public static object DictionaryContent(EntryDetail e, IEnumerable<MediaAsset> media)
    {
        var assets = media.Where(m => m.EntryId == e.Id).OrderBy(m => m.Id).ToList();
        // Both formats enforce the same text/position checks before returning a download.
        _ = new XmlEntry(e, assets).Build();
        return new
    {
        SchemaVersion, TextOffsetUnit = "utf16CodeUnit", TextOffsetOrigin = 0, TextNormalization = "none",
        e.Id, e.Lemma, e.Pinyins, e.ComplexForms,
        Homonyms = e.Homonyms.Select(h => new {
            h.Id, h.Text, LabelMode = h.PinyinId.HasValue ? "pinyin" : "independent", h.PinyinId,
            Markers = Markers(h.Markers), CrossReferences = References(h.CrossReferences),
            GramGroups = (h.GramGroups ?? []).Select(GroupContent), DirectSenses = h.DirectSenses.Select(SenseContent)
        }),
        GramGroups = e.GramGroups.Select(GroupContent),
        DirectPoses = e.DirectPoses.Select(p => new { p.Id, p.Pos, Markers = Markers(p.Markers),
            CrossReferences = References(p.CrossReferences), Senses = p.Senses.Select(SenseContent) }),
        Senses = e.Senses.Select(SenseContent),
        Media = assets.Select(m => new {
            m.Id, MediaId = MediaKey(m), m.EntryId, ParentSegmentId = MediaParent(m), m.StorageUrl,
            m.MimeType, m.OriginalFilename, m.DisplayName, m.Placement, Position = ExportMediaPosition(m, e)
        })
        };
    }
    private static object GroupContent(GramGroupDto g) => new { g.Id, g.PosSegmentId, g.Number, g.Pos,
        Markers = Markers(g.Markers), CrossReferences = References(g.CrossReferences), Senses = g.Senses.Select(SenseContent) };
    private static object SenseContent(SenseDto s)
    {
        var onlyXr = IsCrossReferenceOnly(s);
        return new {
            s.Id, s.DefinitionId, s.Number, Definition = onlyXr ? null : s.Definition,
            DisplayText = s.Definition, DefinitionKind = onlyXr ? "crossReferenceOnly" : string.IsNullOrWhiteSpace(s.Definition) ? "empty" : "definition",
            Markers = Markers(s.Markers, onlyXr),
            CrossReferences = References(s.CrossReferences, onlyXr),
            Examples = s.Examples.Select(e => new { e.Id, e.ZhSegmentId, e.KaSegmentId, e.Zh, e.Ka,
                Markers = Markers(e.Markers), CrossReferences = References(e.CrossReferences) })
        };
    }
    private static IEnumerable<object> Markers(List<MarkerDto> markers, bool onlyXr = false) => markers.Select(m => (object)new {
        m.Id, m.Category, m.Value, m.DisplayText, m.TextOffset,
        TextLength = m.TextField is null ? null : (int?)(m.DisplayText ?? m.Value).Length,
        TextField = onlyXr && m.TextField == "definition" ? "displayText" : m.TextField
    });
    private static IEnumerable<object> References(List<CrossReferenceDto> refs, bool onlyXr = false) => refs.Select(r => (object)new {
        r.Id, r.Type, r.Label, r.Target, r.TargetEntryId, r.TargetEntryLemma, r.DisplayText, r.Placement,
        r.TextOffset, TextLength = r.Placement == "Inline" ? (int?)ReferenceText(r).Length : null,
        TextField = onlyXr && r.TextField == "definition" ? "displayText" : r.TextField
    });
    public static bool IsCrossReferenceOnly(SenseDto s)
    {
        if (!EntryPublicationValidator.HasValidReference(s)) return false;
        if (string.IsNullOrWhiteSpace(s.Definition)) return true;
        var ranges = s.CrossReferences.Where(r => r.Placement == "Inline" && r.TextField == "definition")
            .Select(r => (Offset: r.TextOffset ?? -1, Text: ReferenceText(r))).OrderBy(r => r.Offset).ToList();
        var cursor = 0;
        foreach (var range in ranges)
        {
            if (range.Offset < cursor || range.Offset > s.Definition.Length - range.Text.Length ||
                !s.Definition.AsSpan(range.Offset, range.Text.Length).SequenceEqual(range.Text.AsSpan()) ||
                !string.IsNullOrWhiteSpace(s.Definition[cursor..range.Offset])) return false;
            cursor = range.Offset + range.Text.Length;
        }
        return ranges.Count > 0 && string.IsNullOrWhiteSpace(s.Definition[cursor..]);
    }
    private static string ReferenceText(CrossReferenceDto r) => r.DisplayText ?? r.Label;
    private static string MediaKey(MediaAsset m) => string.IsNullOrEmpty(m.SafeStorageName) ? $"media-{m.Id}" : m.SafeStorageName;
    private sealed record Position(string Kind, int NodeId, string Field, int Offset, string Before = "", string After = "");
    private static IEnumerable<SenseDto> AllSenses(EntryDetail e) => e.Senses.Concat(e.GramGroups.SelectMany(g => g.Senses))
        .Concat(e.DirectPoses.SelectMany(p => p.Senses)).Concat(e.Homonyms.SelectMany(h => h.DirectSenses.Concat((h.GramGroups ?? []).SelectMany(g => g.Senses))));
    private static int? MediaParent(MediaAsset m)
    {
        if (m.InsertedIntoSegmentId.HasValue) return m.InsertedIntoSegmentId;
        if (MediaPosition(m) is { } p) return p.NodeId;
        var placement = m.Placement?.Split(':');
        return placement is { Length: 2 } && placement[0] is "sense" or "example" && int.TryParse(placement[1], out var id) && id > 0 ? id : null;
    }
    private static Position? ExportMediaPosition(MediaAsset m, EntryDetail e)
    {
        var position = MediaPosition(m, e);
        return position is { Kind: "sense", Field: "definition" } && AllSenses(e).Any(s => s.Id == position.NodeId && IsCrossReferenceOnly(s))
            ? position with { Field = "displayText" } : position;
    }
    private static Position? MediaPosition(MediaAsset m, EntryDetail? entry = null)
    {
        if (m.Placement?.StartsWith("inline:", StringComparison.Ordinal) != true) return null;
        try
        {
            var position = JsonSerializer.Deserialize<Position>(m.Placement[7..], new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (position is null || position.Before is null || position.After is null || position.NodeId <= 0 || position.Offset < 0 ||
                !(position.Kind == "sense" && position.Field == "definition" || position.Kind == "example" && position.Field is "zh" or "ka"))
                throw new EntryValidationException($"Media {m.Id} has an invalid inline position.");
            if (entry is null) return position;
            var senses = AllSenses(entry).ToList();
            var text = position.Kind == "sense" ? senses.FirstOrDefault(s => s.Id == position.NodeId)?.Definition
                : senses.SelectMany(s => s.Examples).Where(e => e.Id == position.NodeId).Select(e => position.Field == "zh" ? e.Zh : e.Ka).FirstOrDefault();
            if (text is null) throw new EntryValidationException($"Media {m.Id}: target segment {position.NodeId} is missing.");
            // Match the editor's context anchors after text edits; consumers receive the resolved offset.
            foreach (var (context, delta) in new[] { (position.Before + position.After, position.Before.Length), (position.After, 0), (position.Before, position.Before.Length) })
            {
                if (string.IsNullOrEmpty(context)) continue;
                var index = text.IndexOf(context, StringComparison.Ordinal);
                if (index >= 0 && text.IndexOf(context, index + 1, StringComparison.Ordinal) < 0)
                    return position with { Offset = index + delta };
            }
            return position with { Offset = Math.Min(text.Length, position.Offset) };
        }
        catch (JsonException) { throw new EntryValidationException($"Media {m.Id} has an invalid inline position."); }
    }
    public static string Xml(IEnumerable<EntryDetail> entries) => Xml(entries, []);
    public static string Xml(IEnumerable<EntryDetail> entries, IEnumerable<MediaAsset> media)
    {
        var assets = media.ToLookup(m => m.EntryId);
        return new XDocument(new XElement(Tei + "TEI",
            new XElement(Tei + "teiHeader", new XElement(Tei + "fileDesc",
                new XElement(Tei + "titleStmt", new XElement(Tei + "title", "CHINESE.GE Dictionary")),
                new XElement(Tei + "editionStmt", new XElement(Tei + "edition", $"CHINESE.GE export {SchemaVersion}")),
                new XElement(Tei + "publicationStmt", new XElement(Tei + "p", "Editorial database export")),
                new XElement(Tei + "sourceDesc", new XElement(Tei + "p", "Structured dictionary entries")))),
            new XElement(Tei + "text", new XElement(Tei + "body", entries.Select(e => new XmlEntry(e, assets[e.Id].ToList()).Build()))))).ToString();
    }
    private static XAttribute? Identity(int? id) => id is int key ? new XAttribute(XNamespace.Xml + "id", $"segment-{key}") : null;
    private static XElement Reference(CrossReferenceDto r) => new(Tei + "xr", Identity(r.Id), new XAttribute("type", r.Type),
        new XElement(Tei + "ref", new XAttribute("target", r.TargetEntryId is int id ? $"#entry-{id}" : r.Target), ReferenceText(r)));

    private sealed class XmlEntry(EntryDetail entry, List<MediaAsset> media)
    {
        private readonly HashSet<int> renderedMedia = [];
        private readonly HashSet<CrossReferenceDto> renderedReferences = [];
        private readonly HashSet<MarkerDto> renderedMarkers = [];
        public XElement Build()
        {
            foreach (var asset in media) _ = MediaPosition(asset, entry);
            foreach (var homonym in entry.Homonyms)
                if (homonym.PinyinId is int linked && !entry.Pinyins.Any(p => p.Id == linked))
                    throw new EntryValidationException($"Homograph {homonym.Id}: linked Pinyin {linked} is missing.");
            var result = new XElement(Tei + "entryFree", new XAttribute(XNamespace.Xml + "id", $"entry-{entry.Id}"),
                new XElement(Tei + "form", new XElement(Tei + "orth", entry.Lemma), entry.Pinyins.Select(p =>
                    new XElement(Tei + "pron", new XAttribute(XNamespace.Xml + "id", $"entry-{entry.Id}-pinyin-{p.Id}"), p.Value))),
                entry.ComplexForms.Select(f => new XElement(Tei + "form", new XAttribute("type", "traditional"),
                    new XElement(Tei + "orth", new XAttribute(XNamespace.Xml + "id", $"entry-{entry.Id}-traditional-{f.Id}"), f.Value))),
                entry.Homonyms.Select(h => new XElement(Tei + "hom", Identity(h.Id), h.PinyinId is int id
                    ? new XElement(Tei + "form", new XElement(Tei + "pron", new XAttribute("corresp", $"#entry-{entry.Id}-pinyin-{id}"), h.Text))
                    : new XElement(Tei + "label", new XAttribute("type", "independent"), h.Text),
                    Decorations(h.Markers, h.CrossReferences), (h.GramGroups ?? []).Select(Group), h.DirectSenses.Select(Sense))),
                entry.GramGroups.Select(Group), entry.DirectPoses.Select(p => new XElement(Tei + "entryFree", Identity(p.Id),
                    new XElement(Tei + "gramGrp", new XElement(Tei + "pos", p.Pos)), Decorations(p.Markers, p.CrossReferences), p.Senses.Select(Sense))),
                entry.Senses.Select(Sense));
            result.Add(media.Where(m => !renderedMedia.Contains(m.Id)).Select(Figure));
            return result;
        }
        private XElement Group(GramGroupDto g) => new(Tei + "entryFree", Identity(g.Id),
            new XElement(Tei + "gramGrp", g.Pos is null ? null : new XElement(Tei + "pos", Identity(g.PosSegmentId), g.Pos)),
            Decorations(g.Markers, g.CrossReferences), g.Senses.Select(Sense));
        private XElement Sense(SenseDto s)
        {
            var onlyXr = IsCrossReferenceOnly(s);
            if (onlyXr && string.IsNullOrWhiteSpace(s.Definition) && s.CrossReferences.Any(r => r.Placement == "Inline"))
                throw new EntryValidationException($"Sense {s.Id}: inline reference has no display text field. Use an attached reference or provide its display text.");
            return new XElement(Tei + "sense", Identity(s.Id), new XAttribute("n", s.Number),
                onlyXr && string.IsNullOrWhiteSpace(s.Definition) ? new XElement(Tei + "seg", Identity(s.DefinitionId),
                    new XAttribute("type", "crossReferenceOnly"), s.CrossReferences.Select(Reference))
                    : onlyXr ? Text("seg", s.DefinitionId, s.Id, "definition", s.Definition, s.Markers, s.CrossReferences)
                    : Text("def", s.DefinitionId, s.Id, "definition", s.Definition, s.Markers, s.CrossReferences),
                Decorations(s.Markers, onlyXr && string.IsNullOrWhiteSpace(s.Definition) ? [] : s.CrossReferences), s.Examples.Select(e =>
                    new XElement(Tei + "cit", Identity(e.Id), new XAttribute("type", "example"),
                        Text("quote", e.ZhSegmentId, e.Id, "zh", e.Zh, e.Markers, e.CrossReferences),
                        new XElement(Tei + "cit", new XAttribute("type", "translation"),
                            Text("quote", e.KaSegmentId, e.Id, "ka", e.Ka, e.Markers, e.CrossReferences)), Decorations(e.Markers, e.CrossReferences))));
        }
        private XElement Text(string element, int? id, int? parentId, string field, string text, List<MarkerDto> markers, List<CrossReferenceDto> refs)
        {
            var result = new XElement(Tei + element, Identity(id));
            if (element == "seg") result.Add(new XAttribute("type", "crossReferenceOnly"));
            if (field is "zh" or "ka") result.Add(new XAttribute(XNamespace.Xml + "lang", field));
            if (text.Contains('\n')) result.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
            foreach (var reference in refs.Where(r => r.Placement == "Inline" && r.TextField == field)) renderedReferences.Add(reference);
            foreach (var marker in markers.Where(m => m.TextField == field)) renderedMarkers.Add(marker);
            var spans = refs.Where(r => r.Placement == "Inline" && r.TextField == field)
                .Select(r => (Offset: r.TextOffset ?? -1, Text: ReferenceText(r), Node: Reference(r)))
                .Concat(markers.Where(m => m.TextField == field).Select(m => (Offset: m.TextOffset ?? -1,
                    Text: m.DisplayText ?? m.Value, Node: new XElement(Tei + (m.Category == "Abbr" ? "abbr" : "usg"),
                        Identity(m.Id), new XAttribute("type", m.Category == "Abbr" ? m.Value : m.Category.ToLowerInvariant()), m.DisplayText ?? m.Value))))
                .OrderBy(s => s.Offset).ThenBy(s => s.Text.Length).ToList();
            var cursor = 0;
            foreach (var span in spans)
            {
                if (span.Text.Length == 0 || span.Offset < cursor || span.Offset > text.Length - span.Text.Length ||
                    !text.AsSpan(span.Offset, span.Text.Length).SequenceEqual(span.Text.AsSpan()))
                    throw new EntryValidationException($"Segment {parentId}, field {field}: invalid or overlapping inline position at {span.Offset}.");
                result.Add(new XText(text[cursor..span.Offset]), span.Node);
                cursor = span.Offset + span.Text.Length;
            }
            result.Add(new XText(text[cursor..]));
            foreach (var asset in media.Where(m => MediaPosition(m, entry) is { } p && p.NodeId == parentId && p.Field == field)
                .OrderByDescending(m => MediaPosition(m, entry)!.Offset).ThenByDescending(m => m.Id))
            {
                var offset = MediaPosition(asset, entry)!.Offset;
                var consumed = 0;
                foreach (var node in result.DescendantNodes().OfType<XText>().Where(t => !t.Ancestors(Tei + "figure").Any()).ToList())
                {
                    if (offset <= consumed + node.Value.Length)
                    {
                        var local = offset - consumed;
                        node.ReplaceWith(new XText(node.Value[..local]), Figure(asset), new XText(node.Value[local..]));
                        break;
                    }
                    consumed += node.Value.Length;
                }
            }
            return result;
        }
        private XElement Figure(MediaAsset m)
        {
            renderedMedia.Add(m.Id);
            return new XElement(Tei + "figure", new XAttribute(XNamespace.Xml + "id", $"media-link-{m.Id}"),
                MediaParent(m) is int parent ? new XAttribute("corresp", $"#segment-{parent}") : null,
                new XElement(Tei + "graphic", new XAttribute("url", m.StorageUrl), new XAttribute("mimeType", m.MimeType)),
                new XElement(Tei + "idno", new XAttribute("type", "media"), MediaKey(m)),
                new XElement(Tei + "figDesc", m.DisplayName ?? m.OriginalFilename),
                m.Placement is null ? null : new XElement(Tei + "note", new XAttribute("type", "placement"), m.Placement));
        }
        private IEnumerable<XElement> Decorations(List<MarkerDto> markers, List<CrossReferenceDto> refs)
        {
            if (markers.Any(m => m.TextField is not null && !renderedMarkers.Contains(m)) ||
                refs.Any(r => r.Placement == "Inline" && !renderedReferences.Contains(r)))
                throw new EntryValidationException("An inline annotation has an unsupported parent or text field. Reinsert the annotation in a definition or example.");
            return markers.Where(m => m.TextField is null).Select(m => new XElement(Tei + "usg", Identity(m.Id),
                new XAttribute("type", m.Category.ToLowerInvariant()), m.Value))
            .Concat(refs.Where(r => r.Placement != "Inline").Select(Reference));
        }
    }
}
