using Chiniseapp.Application.Legacy;

namespace Chiniseapp.Tests;

public class LegacyParserTests
{
    [Theory]
    [InlineData(" 名 სახელი ")]
    [InlineData("I 名 1 სიტყვა 2 სხვა 〈口〉 【医】 იხ. 中国")]
    [InlineData("｛hé 1｝\r\n中文：ქართული 😀 & #")]
    [InlineData("")]
    public void TokensPreserveEveryCharacterAndOriginalOffsets(string text)
    {
        var tokens = LegacyParser.Tokenize(text);
        Assert.Equal(text, string.Concat(tokens.Select(t => t.Text)));
        foreach (var token in tokens)
            Assert.Equal(token.Text, text.Substring(token.Offset, token.Text.Length));
    }

    [Fact]
    public void SimplePosDefinitionsProduceTargetDtoWithoutArtificialHomograph()
    {
        var result = LegacyParser.Parse(new("1", "字", " 名 ასო 动 წერა"));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.ProposedContent);
        Assert.Empty(result.ProposedContent.Homonyms);
        Assert.Equal(new[] { "名", "动" }, result.ProposedContent.GramGroups.Select(g => g.Pos));
        Assert.Equal("ასო", result.ProposedContent.GramGroups[0].Senses.Single().Definition);
    }

    [Fact]
    public void ReferenceDelimitersAreMatchedAsWholeRunsAndEmptyGroupsSurvive()
    {
        var result = LegacyParser.Parse(new("1", "字", "名 ასო", "en**zh***ru****extra*****info"));
        Assert.Equal(new LegacyReference("en", "zh", "ru", "extra", "info"), result.ReferenceMaterial);
        var empty = LegacyParser.Parse(new("2", "另", "名 სხვა", "en＊＊＊＊＊info"));
        Assert.Equal(new LegacyReference("en", "", "", "", "info"), empty.ReferenceMaterial);
    }

    [Theory]
    [InlineData("one***three**two")]
    [InlineData("one**two**repeat")]
    [InlineData("one******six")]
    public void InvalidReferenceRunsAreReportedAndRetained(string raw)
    {
        var result = LegacyParser.Parse(new("1", "字", " 名 ასო", raw));
        Assert.Contains(result.Issues, i => i.Code == "InvalidReferenceDelimiter");
        Assert.Equal(raw, result.Source.WorkingSet);
    }

    [Fact]
    public void CommentsPreserveMarkersAndMultilineTextAndReportUnpairedMarker()
    {
        const string raw = "text &Author& #note\nmore# end &";
        var result = LegacyParser.Parse(new("1", "字", " 名 ასო", Comment: raw));
        Assert.Equal(raw, string.Concat(result.CommentParts.Select(p => p.Text)));
        Assert.Contains(result.CommentParts, p => p.Kind == "Author" && p.Text == "&Author&");
        Assert.Contains(result.CommentParts, p => p.Kind == "Note" && p.Text.Contains('\n'));
        Assert.Contains(result.Issues, i => i.Code == "UnpairedCommentMarker");
    }

    [Theory]
    [InlineData(" 名 სიტყვა 中国 ქართული", "ChineseRunNeedsClassification")]
    [InlineData(" 名 იხ. 中国", "CrossReferenceNeedsResolution")]
    [InlineData(" I 名 სიტყვა", "RomanGroupNeedsReview")]
    [InlineData("{hé 1} 名 სიტყვა", "HomographBoundaryNeedsReview")]
    [InlineData(" 名 2026 წელი", "NumberBoundaryNeedsReview")]
    [InlineData(" 名 〈口〉 სიტყვა", "MarkerVocabularyAndScopeNeedsReview")]
    [InlineData(" 名 【医 სიტყვა", "UnmatchedBracket")]
    public void AmbiguousBoundariesNeverBecomeSemanticProposals(string raw, string code)
    {
        var result = LegacyParser.Parse(new("1", "字", raw));
        Assert.Null(result.ProposedContent);
        Assert.Contains(result.Issues, i => i.Code == code);
        Assert.Equal(raw, result.Source.Translation);
    }

    [Fact]
    public void DuplicateHeadwordsBlockBothProposalsAndKeepBothSources()
    {
        var results = LegacyParser.ParseBatch([
            new("1", "字，文", " 名 ასო"), new("2", "字,文", " 名 ტექსტი")]);
        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Null(r.ProposedContent);
            Assert.Contains(r.Issues, i => i.Code == "DuplicateHeadword" && i.Fragment == "1, 2");
        });
    }
}
