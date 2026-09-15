using System.Xml.Linq;
using Chiniseapp.Application.Entries;

namespace Chiniseapp.Infrastructure.Entries;

public static class EntryExportSerializer
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";
    public static object DictionaryContent(EntryDetail e) => new { e.Id, e.Lemma, e.Pinyins, e.ComplexForms, e.Homonyms, e.GramGroups, e.DirectPoses, e.Senses };
    public static string Xml(IEnumerable<EntryDetail> entries) => new XDocument(
        new XElement(Tei + "TEI", new XElement(Tei + "teiHeader", new XElement(Tei + "fileDesc",
            new XElement(Tei + "titleStmt", new XElement(Tei + "title", "CHINESE.GE Dictionary")),
            new XElement(Tei + "publicationStmt", new XElement(Tei + "p", "Editorial database export")),
            new XElement(Tei + "sourceDesc", new XElement(Tei + "p", "Structured dictionary entries")))),
            new XElement(Tei + "text", new XElement(Tei + "body", entries.Select(Entry))))).ToString();
    private static XElement Entry(EntryDetail e) => new(Tei + "entryFree", new XAttribute(XNamespace.Xml + "id", $"entry-{e.Id}"),
        new XElement(Tei + "form", new XElement(Tei + "orth", e.Lemma), e.Pinyins.Select(p => new XElement(Tei + "pron", p.Value))),
        e.ComplexForms.Select(f => new XElement(Tei + "form", new XAttribute("type", "traditional"), new XElement(Tei + "orth", f.Value))),
        e.Homonyms.Select(h => new XElement(Tei + "hom", Identity(h.Id), new XElement(Tei + "form", new XElement(Tei + "orth", h.Text)),
            Decorations(h.Markers,h.CrossReferences), (h.GramGroups ?? []).Select(Group), h.DirectSenses.Select(Sense))),
        e.GramGroups.Select(Group), e.DirectPoses.Select(p => new XElement(Tei + "entryFree", Identity(p.Id),
            new XElement(Tei + "gramGrp", new XElement(Tei + "pos", p.Pos)), Decorations(p.Markers,p.CrossReferences), p.Senses.Select(Sense))), e.Senses.Select(Sense));
    private static XAttribute? Identity(int? id) => id is int key ? new XAttribute(XNamespace.Xml + "id", $"segment-{key}") : null;
    private static XElement Group(GramGroupDto g) => new(Tei + "entryFree", Identity(g.Id),
        new XElement(Tei + "gramGrp", g.Pos is null ? null : new XElement(Tei + "pos", g.Pos)), Decorations(g.Markers,g.CrossReferences), g.Senses.Select(Sense));
    private static XElement Sense(SenseDto s) => new(Tei + "sense", Identity(s.Id), new XAttribute("n", s.Number),
        new XElement(Tei + "def", s.Definition), Decorations(s.Markers,s.CrossReferences), s.Examples.Select(e =>
            new XElement(Tei + "cit", Identity(e.Id), new XAttribute("type","example"),
                new XElement(Tei + "quote", new XAttribute(XNamespace.Xml + "lang","zh"), e.Zh),
                new XElement(Tei + "cit", new XAttribute("type","translation"), new XElement(Tei + "quote", new XAttribute(XNamespace.Xml + "lang","ka"), e.Ka)), Decorations(e.Markers,e.CrossReferences))));
    private static IEnumerable<XElement> Decorations(List<MarkerDto> markers, List<CrossReferenceDto> references) =>
        markers.Select(m => new XElement(Tei + "usg", Identity(m.Id), new XAttribute("type",m.Category.ToLowerInvariant()),m.Value))
            .Concat(references.Select(r => new XElement(Tei + "xr", Identity(r.Id), new XAttribute("type",r.Type),
                new XElement(Tei + "ref", new XAttribute("target", r.TargetEntryId is int id ? $"#entry-{id}" : r.Target), r.DisplayText ?? r.Label))));
}

