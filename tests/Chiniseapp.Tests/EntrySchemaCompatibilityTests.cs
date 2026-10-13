using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class EntrySchemaCompatibilityTests
{
    [Fact]
    public void HomographPronunciationIdentitySurvivesReloadRenameAndReorder()
    {
        var request = new SaveEntryContentRequest(null, null, [new HomonymDto([]) { PinyinId = 2, Text = "stale" }], 0);
        var forms = new LexicalForms([new(1, "hé", 1), new(2, "huó", 0)], []);
        var linked = EntryTreeMapper.LinkPronunciations(request, forms);
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(linked);
        var reloaded = EntryTreeMapper.Read(Persist(mapper));
        Assert.Equal(2, reloaded.Homonyms[0].PinyinId);
        Assert.Equal("huó", reloaded.Homonyms[0].Text);
        var renamed = EntryTreeMapper.LinkPronunciations(reloaded, new([new(2, "huò", 1), new(1, "hé", 0)], []));
        Assert.Equal("huò", renamed.Homonyms[0].Text);
        Assert.Throws<EntryValidationException>(() => EntryTreeMapper.LinkPronunciations(reloaded, new([new(1, "hé", 0)], [])));
    }
    [Fact]
    public void NewPronunciationLinksResolveToSavedIdsWhileIndependentLegacyLabelsRemain()
    {
        var request = new SaveEntryContentRequest(null, null, [new HomonymDto([]) { PinyinOrder = 0 }, new HomonymDto([]) { Text = "legacy" }], 0);
        var linked = EntryTreeMapper.LinkPronunciations(request, new([new(7, "hé", 0)], []));
        Assert.Equal(7, linked.Homonyms[0].PinyinId);
        Assert.Null(linked.Homonyms[0].PinyinOrder);
        Assert.Equal("legacy", linked.Homonyms[1].Text);
    }
    [Fact]
    public void InlineAnnotationsPreserveTheirFieldPositionAndTextOnReload()
    {
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(new(null, null, [], 0) { Senses = [new(1, "ჩვეულ. იხ. 中国", []) {
            Markers = [new(null, "Abbr", "usual") { TextField = "definition", TextOffset = 0, DisplayText = "ჩვეულ." }],
            CrossReferences = [new(null, "see", "იხ.", "中国", 2, "中国", "იხ. 中国", "Inline") { TextField = "definition", TextOffset = 7 }]
        }] });
        var result = EntryTreeMapper.Read(Persist(mapper));
        Assert.Equal("ჩვეულ.", result.Senses[0].Markers[0].DisplayText);
        Assert.Equal(0, result.Senses[0].Markers[0].TextOffset);
        Assert.Equal("definition", result.Senses[0].CrossReferences[0].TextField);
        Assert.Equal(7, result.Senses[0].CrossReferences[0].TextOffset);
        Assert.Equal(2, result.Senses[0].CrossReferences[0].TargetEntryId);
    }
    [Fact]
    public void WrittenFormsEnforceIndependentLimitsWithoutChangingPersistedData()
    {
        var entry = new Entry { Lemma = "测试" };
        var pinyins = Enumerable.Range(1, 5).Select(i => new FormItemDto(null, $"pinyin{i}", i)).ToList();
        var forms = Enumerable.Range(1, 3).Select(i => new FormItemDto(null, $"form{i}", i)).ToList();
        EntryTreeMapper.SaveForms(entry, pinyins, forms);
        var original = entry.LexicalForms;
        Assert.Throws<EntryValidationException>(() => EntryTreeMapper.SaveForms(entry, [.. pinyins, new(null, "extra", 5)], null));
        Assert.Equal(original, entry.LexicalForms);
        Assert.Throws<EntryValidationException>(() => EntryTreeMapper.SaveForms(entry, null, [.. forms, new(null, "extra", 3)]));
        Assert.Equal(original, entry.LexicalForms);
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static List<Segment> Persist(EntryTreeMapper mapper)
    {
        var next = mapper.Nodes.Select(s => s.Id).DefaultIfEmpty().Max() + 1;
        foreach (var node in mapper.Nodes) if (node.Id == 0) node.Id = next++;
        foreach (var node in mapper.Nodes) node.ParentSegmentId = node.Parent?.Id;
        return mapper.Nodes;
    }

    [Fact]
    public void StructuredContentRoundTripsEverySupportedBranch()
    {
        var request = JsonSerializer.Deserialize<SaveEntryContentRequest>("""
        {"lemma":"测试","rowVersion":1,"homonyms":[{"text":"一","gramGroups":[{"pos":"n","senses":[{"definition":"სახელი","examples":[{"zh":"你好","ka":"გამარჯობა","markers":[{"category":"Style","value":"口"}]}]}]}]}],
        "gramGroups":[{"pos":"v","senses":[]}],"directPoses":[{"pos":"adj","senses":[]}],
        "senses":[{"definition":"ტესტი","examples":[],"crossReferences":[{"type":"seeAlso","label":"参见","target":"中国","targetEntryId":42,"placement":"Attached"}]}]}
        """, Json)!;
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(request);
        var result = EntryTreeMapper.Read(Persist(mapper));
        Assert.Equal("一", result.Homonyms[0].Text);
        Assert.Equal("n", result.Homonyms[0].GramGroups![0].Pos);
        Assert.Equal("口", result.Homonyms[0].GramGroups![0].Senses[0].Examples[0].Markers[0].Value);
        Assert.Equal("გამარჯობა", result.Homonyms[0].GramGroups![0].Senses[0].Examples[0].Ka);
        Assert.Equal("v", result.GramGroups[0].Pos);
        Assert.Equal("adj", result.DirectPoses[0].Pos);
        Assert.Equal(42, result.Senses[0].CrossReferences[0].TargetEntryId);
        Assert.All(mapper.Nodes, n => Assert.True(n.Id > 0));
        var next = new EntryTreeMapper(1, mapper.Nodes, DateTime.UtcNow);
        next.Apply(result);
        Assert.Empty(next.Removed);
        Assert.Equal(mapper.Nodes.Select(n => n.Id).Order(), next.Nodes.Select(n => n.Id).Order());
    }

    [Fact]
    public void LegacyClientsRetainTheirExistingSaveContract()
    {
        var request = new SaveEntryContentRequest("测试", "cèshì", [new HomonymDto([new SenseDto(1, "ტესტი", [])])], 1);
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(request);
        var result = EntryTreeMapper.Read(Persist(mapper));
        Assert.Equal("ტესტი", result.Homonyms[0].Senses![0].Definition);
        Assert.Single(result.Homonyms[0].GramGroups!);
    }

    [Fact]
    public void EditingAndReorderingPreserveIdentity()
    {
        var mapper = new EntryTreeMapper(1, [], DateTime.UtcNow);
        mapper.Apply(new(null, null, [], 0) { Senses = [new(1, "first", []), new(2, "second", [])] });
        var original = EntryTreeMapper.Read(Persist(mapper));
        var next = new EntryTreeMapper(1, mapper.Nodes, DateTime.UtcNow);
        next.Apply(original with { Senses = [original.Senses[1] with { Definition = "changed" }, original.Senses[0]] });
        var result = EntryTreeMapper.Read(Persist(next));
        Assert.Empty(next.Removed);
        Assert.Equal(original.Senses[1].Id, result.Senses[0].Id);
        Assert.Equal(original.Senses[1].DefinitionId, result.Senses[0].DefinitionId);
        Assert.Equal("changed", result.Senses[0].Definition);
        Assert.Equal(1, result.Senses[0].Number);
    }

    [Fact]
    public void ForeignAndDuplicateIdsAreRejected()
    {
        var existing = new Segment { Id = 5, EntryId = 1, SegmentType = SegmentType.Sense };
        foreach (var senses in new List<SenseDto>[] {
            [new(1,"",[]) { Id = 6 }], [new(1,"",[]) { Id = 5 }, new(2,"",[]) { Id = 5 }] })
        {
            var mapper = new EntryTreeMapper(1, [existing], DateTime.UtcNow);
            Assert.Throws<EntryValidationException>(() => mapper.Apply(new(null,null,[],0) { Senses = senses }));
        }
    }

    [Fact]
    public void PronunciationsAndTraditionalFormsRetainOrderAndIdentity()
    {
        var entry = new Entry { Pinyin = "old" };
        EntryTreeMapper.SaveForms(entry, [new(1,"old",0),new(null,"new",1)], [new(null,"傳統",0)]);
        var first = EntryTreeMapper.ReadForms(entry);
        EntryTreeMapper.SaveForms(entry, [first.Pinyins[1], first.Pinyins[0]], null);
        var result = EntryTreeMapper.ReadForms(entry);
        Assert.Equal(first.Pinyins[1].Id, result.Pinyins[0].Id);
        Assert.Equal(0, result.Pinyins[0].Order);
        Assert.Equal("new", entry.Pinyin);
        Assert.Equal("傳統", result.ComplexForms[0].Value);
        Assert.Throws<EntryValidationException>(() => EntryTreeMapper.SaveForms(entry, [new(null,"a",0),new(null,"A",1)], null));
    }

    [Fact]
    public void UnknownTopLevelFieldsAreNotSilentlyDiscarded() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveEntryContentRequest>("""{"homonyms":[],"unsupportedContent":[1]}""", Json));
}
