using System.Text.Json;
using System.Xml.Linq;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class GroupedCrossReferenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    private static CrossReferenceDto Group(List<CrossReferenceTargetDto> targets, int? id = null)
    {
        var text = "ასევე ";
        var positioned = targets.Select((t, i) => {
            if (i > 0) text += ", ";
            var offset = text.Length;
            text += t.DisplayText;
            return t with { TextOffset = offset };
        }).ToList();
        return new(id, "seeAlso", "ასევე", positioned[0].Target, positioned[0].TargetEntryId, null, text, "Inline") {
            TextField = "definition", TextOffset = 0, Targets = positioned
        };
    }
    private static List<Segment> Persist(EntryTreeMapper mapper)
    {
        var next = mapper.Nodes.Select(n => n.Id).DefaultIfEmpty().Max() + 1;
        foreach (var node in mapper.Nodes) if (node.Id == 0) node.Id = next++;
        foreach (var node in mapper.Nodes) node.ParentSegmentId = node.Parent?.Id;
        return mapper.Nodes;
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void GroupSurvivesJsonInputPersistenceReorderAndBothExports(int count)
    {
        string[] words = ["巴儿狗", "叭儿狗", "狮子狗", "中国", "学习"];
        var reference = Group(words.Take(count).Select((word, i) => new CrossReferenceTargetDto(null, word, i + 2, word, word, 0)).ToList());
        var request = new SaveEntryContentRequest(null, null, [], 0) {
            Senses = [new(1, reference.DisplayText!, []) { CrossReferences = [reference] }]
        };
        var parsed = JsonSerializer.Deserialize<SaveEntryContentRequest>(JsonSerializer.Serialize(request, Json), Json)!;
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(parsed);
        var saved = EntryTreeMapper.Read(Persist(mapper));
        var stored = saved.Senses[0].CrossReferences.Single();
        Assert.Equal(count, stored.Targets.Count);
        Assert.All(stored.Targets, t => Assert.False(string.IsNullOrEmpty(t.Id)));
        Assert.Equal(count, stored.Targets.Select(t => t.Id).Distinct().Count());
        var reordered = Group(stored.Targets.AsEnumerable().Reverse().ToList(), stored.Id);
        var second = new EntryTreeMapper(1, mapper.Nodes, DateTime.UtcNow);
        second.Apply(saved with { Senses = [saved.Senses[0] with { Definition = reordered.DisplayText!, CrossReferences = [reordered] }] });
        var reloaded = EntryTreeMapper.Read(Persist(second));
        Assert.Empty(second.Removed);
        Assert.Equal(stored.Id, reloaded.Senses[0].CrossReferences[0].Id);
        Assert.Equal(stored.Targets.Select(t => t.Id).Reverse(), reloaded.Senses[0].CrossReferences[0].Targets.Select(t => t.Id));
        var entry = new EntryDetail(1, "哈巴狗儿", null, "Ready", null, "Editor", DateTime.UtcNow, DateTime.UtcNow, 1, [], new(true, [])) { Senses = reloaded.Senses };
        var json = JsonSerializer.SerializeToElement(EntryExportSerializer.DictionaryContent(entry), Json);
        ExportTestSamples.Write($"grouped-{count}", entry);
        Assert.Equal(count, json.GetProperty("senses")[0].GetProperty("crossReferences")[0].GetProperty("targets").GetArrayLength());
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry]));
        var xr = xml.Descendants(Tei + "xr").Single();
        Assert.Equal(reordered.DisplayText, xr.Value);
        Assert.Equal(count, xr.Elements(Tei + "ref").Count());
        Assert.Equal(stored.Targets.Select(t => t.DisplayText).Reverse(), xr.Elements(Tei + "ref").Select(t => t.Value));
    }

    [Fact]
    public void EditingOneTargetAndRemovingAnotherKeepsTheRemainingIdentities()
    {
        var reference = Group([new(null, "巴儿狗", 2, null, "巴儿狗", 0), new(null, "叭儿狗", 3, null, "叭儿狗", 0), new(null, "狮子狗", 4, null, "狮子狗", 0)]);
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(new(null, null, [], 0) { Senses = [new(1, reference.DisplayText!, []) { CrossReferences = [reference] }] });
        var saved = EntryTreeMapper.Read(Persist(mapper));
        var old = saved.Senses[0].CrossReferences[0];
        var edited = Group([old.Targets[0] with { Target = "中国", TargetEntryId = 9, DisplayText = "中国" }, old.Targets[2]], old.Id);
        var second = new EntryTreeMapper(1, mapper.Nodes, DateTime.UtcNow);
        second.Apply(saved with { Senses = [saved.Senses[0] with { Definition = edited.DisplayText!, CrossReferences = [edited] }] });
        var targets = EntryTreeMapper.Read(Persist(second)).Senses[0].CrossReferences[0].Targets;
        Assert.Equal(new[] { old.Targets[0].Id, old.Targets[2].Id }, targets.Select(t => t.Id));
        Assert.Equal("中国", targets[0].Target);
    }

    [Fact]
    public void ForeignTargetIdentitiesAndInvalidRangesAreRejected()
    {
        var reference = Group([new("foreign", "中国", 2, null, "中国", 0)]);
        Assert.Throws<EntryValidationException>(() => CrossReferenceRules.AssignTargetIdentities(reference, null));
        Assert.Throws<EntryValidationException>(() => CrossReferenceRules.ValidateTargets(reference with {
            Targets = [reference.Targets[0] with { TextOffset = 500 }] }));
    }
    [Fact]
    public void MediaAtTheEndOfAGroupedReferenceStaysOutsideTheGroup()
    {
        var reference = Group([new(null, "巴儿狗", 2, null, "巴儿狗", 0), new(null, "狮子狗", 3, null, "狮子狗", 0)]);
        var entry = new EntryDetail(1, "哈巴狗儿", null, "Ready", null, "Editor", DateTime.UtcNow, DateTime.UtcNow, 1, [], new(true, [])) {
            Senses = [new(1, reference.DisplayText!, []) { Id = 30, DefinitionId = 31, CrossReferences = [reference] }] };
        var media = new MediaAsset { Id = 21, EntryId = 1, InsertedIntoSegmentId = 30, StorageUrl = "/media/21/content", MimeType = "image/png",
            Placement = "inline:" + JsonSerializer.Serialize(new { kind = "sense", nodeId = 30, field = "definition", offset = reference.DisplayText!.Length }) };
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry], [media]));
        var figure = xml.Descendants(Tei + "figure").Single();
        Assert.Equal(Tei + "seg", figure.Parent!.Name);
        Assert.Empty(figure.Ancestors(Tei + "xr"));
        Assert.Single(figure.ElementsBeforeSelf(Tei + "xr"));
    }
}
