using Chiniseapp.Application.Legacy;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Tests;

public class LegacySemanticParserTests
{
    [Fact]
    public void ComplexParserKeepsInlineReferenceOffsetsAfterMarkersAndEmoji()
    {
        const string raw = "名 【民】 კალენდარი 🐯 (იხ. 农历) და (იხ. 生肖):.";
        var sense = LegacySemanticParser.Parse(new("1", "十二生肖", raw), complex: true)
            .ProposedContent!.GramGroups.Single().Senses.Single();
        Assert.Contains("🐯", sense.Definition);
        Assert.Equal(new[] { "农历", "生肖" }, sense.CrossReferences.Select(r => r.Target));
        foreach (var reference in sense.CrossReferences)
        {
            Assert.Equal("Inline", reference.Placement);
            Assert.Equal(reference.DisplayText, sense.Definition.Substring(reference.TextOffset!.Value,
                reference.DisplayText!.Length));
        }
    }

    [Fact]
    public void ComplexParserKeepsPronunciationsAndScopesSeparateForHomographs()
    {
        var result = LegacySemanticParser.Parse(new("1", "咖", "{kā} 音 იხ. 咖啡 :. {gā} 音 იხ. 咖喱 :.",
            Transcription: "kā; gā"), complex: true).ProposedContent!;
        Assert.Equal(new int?[] { 0, 1 }, result.Homonyms.Select(h => h.PinyinOrder));
        Assert.Equal(new[] { "咖啡", "咖喱" }, result.Homonyms.Select(h =>
            h.GramGroups!.Single().Senses.Single().CrossReferences.Single().Target));
        Assert.Empty(result.GramGroups);
    }

    [Fact]
    public void FormsAndDefinitionTerminatorAreConvertedWithoutInventingHomographs()
    {
        var r = LegacySemanticParser.Parse(new("1", "词", "名 სიტყვა:.", Transcription: "cí", Traditional: "詞"));
        var p = Assert.IsType<Chiniseapp.Application.Entries.SaveEntryContentRequest>(r.ProposedContent);
        Assert.Empty(p.Homonyms);
        Assert.Equal("cí", p.Pinyins!.Single().Value);
        Assert.Equal("詞", p.ComplexForms!.Single().Value);
        Assert.Equal("სიტყვა", p.GramGroups.Single().Senses.Single().Definition);
        Assert.Empty(p.GramGroups.Single().Senses.Single().Examples);
        Assert.Equal("名 სიტყვა:.", r.Source.Translation);
    }

    [Fact]
    public void RomanPosGroupsNumberedSensesAndMarkerScopeAreIndependent()
    {
        var p = LegacySemanticParser.Parse(new("1", "词", "I 名 〈书〉 1 ერთი:. 2 【医】 ორი:. II 动 მოქმედება:.")).ProposedContent!;
        Assert.Equal(2, p.GramGroups.Count);
        Assert.Equal("书", p.GramGroups[0].Markers.Single().Value);
        Assert.Equal(2, p.GramGroups[0].Senses.Count);
        Assert.Empty(p.GramGroups[0].Senses[0].Markers);
        Assert.Equal("医", p.GramGroups[0].Senses[1].Markers.Single().Value);
        Assert.Empty(p.Homonyms);
    }

    [Fact]
    public void ChineseTargetsInsideReferencesAreNotExtraExamples()
    {
        var r = LegacySemanticParser.Parse(new("1", "词", "名 განმარტება: 中文 ქართული 中国 (იხ. 中国)"));
        var examples = r.ProposedContent!.GramGroups.Single().Senses.Single().Examples;
        Assert.Equal(2, examples.Count);
        Assert.Equal("ქართული", examples[0].Ka);
        Assert.Empty(examples[1].Ka);
        Assert.Equal("中国", examples[1].CrossReferences.Single().Target);
        Assert.Contains(r.Issues, i => i.Code == "IncompleteExample");
    }

    [Fact]
    public void MultiTargetReferenceOffsetsValidateAndSurviveMapperRoundTrip()
    {
        var p = LegacySemanticParser.Parse(new("1", "词", "(ასევე 中国, 中文) 名 განმარტება:.")).ProposedContent!;
        var xr = p.GramGroups.Single().CrossReferences.Single();
        CrossReferenceRules.ValidateTargets(xr);
        Assert.Equal(new[] { "中国", "中文" }, xr.Targets.Select(t => t.Target));
        var mapper = new EntryTreeMapper(123, [], DateTime.UtcNow);
        mapper.Apply(p);
        var key = 1;
        foreach (var node in mapper.Nodes) node.Id = key++;
        foreach (var node in mapper.Nodes) node.ParentSegmentId = node.Parent?.Id;
        var restored = EntryTreeMapper.Read(mapper.Nodes);
        Assert.Equal("განმარტება", restored.GramGroups.Single().Senses.Single().Definition);
        Assert.Equal(2, restored.GramGroups.Single().CrossReferences.Single().Targets.Count);
    }

    [Fact]
    public void StyleOnlyReferenceSenseHasNoArtificialPos()
    {
        var r = LegacySemanticParser.Parse(new("1", "词", "〈书〉იგივეა, რაც 中国"));
        var p = r.ProposedContent!;
        Assert.Empty(p.GramGroups);
        Assert.Equal("书", p.Senses.Single().Markers.Single().Value);
        Assert.Empty(p.Senses.Single().Definition);
        Assert.Equal("equivalent", p.Senses.Single().CrossReferences.Single().Type);
        Assert.Contains(r.Issues, i => i.Code == "MissingDefinitionTerminator");
    }

    [Theory]
    [InlineData("{hé} 名 ტექსტი:.")]
    [InlineData("名 1 ერთი:. 3 სამი:.")]
    [InlineData("名 ტექსტი: ქართული ჩინური საწყისის გარეშე")]
    [InlineData("名 ტექსტი _1:.")]
    public void UnsupportedOrAmbiguousBoundariesBlockProposals(string text)
    {
        Assert.Null(LegacySemanticParser.Parse(new("1", "词", text)).ProposedContent);
    }
}
