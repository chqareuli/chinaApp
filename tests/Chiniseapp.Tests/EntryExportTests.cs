using System.Text.Json;
using System.Xml.Linq;
using Chiniseapp.Application.Entries;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Tests;

public class EntryExportTests
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";
    private static EntryDetail Entry() => new(1, "测试", null, "Ready", null, "Private editor", DateTime.UtcNow, DateTime.UtcNow, 1, [], new(true, []));
    private static JsonElement Json(EntryDetail entry, params MediaAsset[] media) =>
        JsonSerializer.SerializeToElement(EntryExportSerializer.DictionaryContent(entry, media), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void JsonHasOneCanonicalSensePathAndExplicitHomographModes()
    {
        var sense = new SenseDto(1, "განმარტება", []) { Id = 30 };
        var entry = Entry() with { Pinyins = [new(2, "cè", 0)], Homonyms = [
            new([sense]) { Id = 10, Text = "cè", PinyinId = 2, GramGroups = [new() { Id = 20, Pos = "n", Senses = [sense] }] },
            new([]) { Id = 11, Text = "დამოუკიდებელი სათაური" }
        ] };
        var json = Json(entry);
        Assert.False(json.GetProperty("homonyms")[0].TryGetProperty("senses", out _));
        Assert.Equal(30, json.GetProperty("homonyms")[0].GetProperty("gramGroups")[0].GetProperty("senses")[0].GetProperty("id").GetInt32());
        Assert.Equal("pinyin", json.GetProperty("homonyms")[0].GetProperty("labelMode").GetString());
        Assert.Equal("independent", json.GetProperty("homonyms")[1].GetProperty("labelMode").GetString());
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry]));
        Assert.Single(xml.Descendants(Tei + "sense"));
        Assert.Equal("#entry-1-pinyin-2", xml.Descendants(Tei + "hom").First().Descendants(Tei + "pron").Single().Attribute("corresp")!.Value);
        Assert.Equal("დამოუკიდებელი სათაური", xml.Descendants(Tei + "label").Single().Value);
    }

    [Fact]
    public void InlineReferencesAndAbbreviationsStayAtTheirExactPositionsWithoutRepeatedText()
    {
        const string text = "😀 中国 და მაგ. 中国\n① ბოლო 中国";
        var offsets = new[] { text.IndexOf("中国", StringComparison.Ordinal), text.IndexOf("中国", 6, StringComparison.Ordinal), text.LastIndexOf("中国", StringComparison.Ordinal) };
        var refs = offsets.Select((offset, i) => new CrossReferenceDto(100 + i, "see", "იხ.", "中国", 2, null, "中国", "Inline") { TextField = "definition", TextOffset = offset }).ToList();
        var entry = Entry() with { Senses = [new(1, text, []) { Id = 30, DefinitionId = 31, CrossReferences = refs,
            Markers = [new(40, "Abbr", "example") { DisplayText = "მაგ.", TextField = "definition", TextOffset = text.IndexOf("მაგ.", StringComparison.Ordinal) }] }] };
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry]));
        var def = xml.Descendants(Tei + "def").Single();
        Assert.Equal(text, def.Value);
        Assert.Equal(3, def.Elements(Tei + "xr").Count());
        Assert.Equal("მაგ.", def.Element(Tei + "abbr")!.Value);
        Assert.Equal("example", def.Element(Tei + "abbr")!.Attribute("type")!.Value);
        Assert.Empty(xml.Descendants(Tei + "usg"));
        Assert.Equal("preserve", def.Attribute(XNamespace.Xml + "space")!.Value);
        var json = Json(entry);
        Assert.Equal("utf16CodeUnit", json.GetProperty("textOffsetUnit").GetString());
        Assert.All(json.GetProperty("senses")[0].GetProperty("crossReferences").EnumerateArray(), r => Assert.Equal(2, r.GetProperty("textLength").GetInt32()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("იგივეა, რაც 圣诞节")]
    public void ReferenceOnlySenseIsNotExportedAsAnIndependentDefinition(string text)
    {
        var reference = new CrossReferenceDto(44, "equivalent", "იგივეა, რაც", "圣诞节", 9, null, "იგივეა, რაც 圣诞节", text.Length == 0 ? "Attached" : "Inline")
            { TextField = text.Length == 0 ? null : "definition", TextOffset = text.Length == 0 ? null : 0 };
        var entry = Entry() with { Senses = [new(1, text, []) { Id = 30, CrossReferences = [reference] }] };
        var sense = Json(entry).GetProperty("senses")[0];
        Assert.Equal(JsonValueKind.Null, sense.GetProperty("definition").ValueKind);
        Assert.Equal("crossReferenceOnly", sense.GetProperty("definitionKind").GetString());
        Assert.Equal(text, sense.GetProperty("displayText").GetString());
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry]));
        Assert.Empty(xml.Descendants(Tei + "def"));
        Assert.Single(xml.Descendants(Tei + "xr"));
    }

    [Fact]
    public void SharedMediaIdentityPlacementAndResolvedOffsetsSurviveBothExports()
    {
        var entry = Entry() with { Senses = [new(1, "new before after", []) { Id = 30 }] };
        var media = new MediaAsset { Id = 7, EntryId = 1, SafeStorageName = "shared-file", StorageUrl = "https://dictionary.test/media/7/content", MimeType = "image/png",
            DisplayName = "Art of War", InsertedIntoSegmentId = 30,
            Placement = "inline:{\"kind\":\"sense\",\"nodeId\":30,\"field\":\"definition\",\"offset\":7,\"before\":\"before \",\"after\":\"after\"}" };
        var linked = new MediaAsset { Id = 8, EntryId = 2, SafeStorageName = "shared-file", StorageUrl = "https://dictionary.test/media/8/content", MimeType = "image/png", Placement = "cover" };
        var json = Json(entry, media, linked);
        Assert.Single(json.GetProperty("media").EnumerateArray());
        Assert.Equal(11, json.GetProperty("media")[0].GetProperty("position").GetProperty("offset").GetInt32());
        Assert.Equal("shared-file", json.GetProperty("media")[0].GetProperty("mediaId").GetString());
        var second = Entry() with { Id = 2 };
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry, second], [media, linked]));
        Assert.Equal(2, xml.Descendants(Tei + "figure").Count());
        Assert.All(xml.Descendants(Tei + "idno"), id => Assert.Equal("shared-file", id.Value));
        var def = xml.Descendants(Tei + "def").Single();
        Assert.Equal("new before ", ((XText)def.Nodes().First()).Value);
        Assert.Equal("#segment-30", def.Element(Tei + "figure")!.Attribute("corresp")!.Value);
        var individual = XDocument.Parse(EntryExportSerializer.Xml([entry], [media]));
        Assert.True(XNode.DeepEquals(individual.Descendants(Tei + "entryFree").First(), xml.Descendants(Tei + "entryFree").First()));
    }

    [Fact]
    public void InvalidInlineOffsetsFailWithTheParentAndFieldInsteadOfDroppingLinks()
    {
        var entry = Entry() with { Senses = [new(1, "中国", []) { Id = 30,
            CrossReferences = [new(44, "see", "", "中国", 2, null, "中国", "Inline") { TextOffset = 9, TextField = "definition" }] }] };
        var error = Assert.Throws<EntryValidationException>(() => EntryExportSerializer.Xml([entry]));
        Assert.Contains("30", error.Message);
        Assert.Contains("definition", error.Message);
        Assert.Throws<EntryValidationException>(() => Json(entry));
    }

    [Fact]
    public void ExampleAnnotationsAndMediaInsideReferencePreserveBothLanguageSegments()
    {
        var entry = Entry() with { Senses = [new(1, "definition", [new("中国", "ჩინეთი") {
            Id = 50, ZhSegmentId = 51, KaSegmentId = 52,
            CrossReferences = [new(60, "see", "", "中国", 2, null, "中国", "Inline") { TextField = "zh", TextOffset = 0 }],
            Markers = [new(61, "Abbr", "China") { TextField = "ka", TextOffset = 0, DisplayText = "ჩინეთი" }]
        }]) { Id = 30 }] };
        var media = new MediaAsset { Id = 7, EntryId = 1, SafeStorageName = "image", StorageUrl = "/media/7/content", MimeType = "image/png",
            Placement = "inline:{\"kind\":\"example\",\"nodeId\":50,\"field\":\"zh\",\"offset\":1}" };
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry], [media]));
        var quotes = xml.Descendants(Tei + "quote").ToArray();
        Assert.Equal("segment-51", quotes[0].Attribute(XNamespace.Xml + "id")!.Value);
        Assert.Equal("segment-52", quotes[1].Attribute(XNamespace.Xml + "id")!.Value);
        Assert.Single(quotes[0].Descendants(Tei + "ref").Single().Elements(Tei + "figure"));
        Assert.Equal("ჩინეთი", quotes[1].Element(Tei + "abbr")!.Value);
        Assert.Equal(50, Json(entry, media).GetProperty("media")[0].GetProperty("parentSegmentId").GetInt32());
    }

    [Fact]
    public void SixPronunciationsAndMarkerOrderSurviveExport()
    {
        var entry = Entry() with { Pinyins = Enumerable.Range(1, 6).Select(i => new FormItemDto(i, $"pinyin{i}", i - 1)).ToList(),
            GramGroups = [new() { Id = 20, Pos = "名", Markers = [new(21, "Style", "粗"), new(22, "Style", "成"), new(23, "Domain", "动"), new(24, "Domain", "船")] }] };
        Assert.Equal(6, Json(entry).GetProperty("pinyins").GetArrayLength());
        var xml = XDocument.Parse(EntryExportSerializer.Xml([entry]));
        Assert.Equal(6, xml.Descendants(Tei + "pron").Count());
        Assert.Equal(new[] { "粗", "成", "动", "船" }, xml.Descendants(Tei + "usg").Select(m => m.Value));
    }
    [Fact]
    public void XmlEscapesTextAndPreservesChineseGeorgianAndReferences()
    {
        var entry = new EntryDetail(1,"测<&",null,"Ready",null,"Private editor",DateTime.UtcNow,DateTime.UtcNow,1,[],new(true,[]))
        {
            Pinyins = [new(1,"cè",0)], ComplexForms = [new(1,"測",0)],
            Senses = [new(1,"ტესტი & <ტექსტი>",[new("你好","გამარჯობა")]) { Id = 3,
                CrossReferences = [new(4,"see","参见","中国",2,null,null,"Attached")] }]
        };
        var xml = EntryExportSerializer.Xml([entry]);
        var document = XDocument.Parse(xml);
        XNamespace ns = "http://www.tei-c.org/ns/1.0";
        Assert.Equal("ტესტი & <ტექსტი>", document.Descendants(ns+"def").Single().Value);
        Assert.Equal("#entry-2", document.Descendants(ns+"ref").Single().Attribute("target")!.Value);
        Assert.Contains("გამარჯობა",xml);
        Assert.DoesNotContain("Private editor",xml);
        var json = JsonSerializer.Serialize(EntryExportSerializer.DictionaryContent(entry));
        Assert.DoesNotContain("Private editor",json);
        Assert.DoesNotContain("Capabilities",json);
    }
}
