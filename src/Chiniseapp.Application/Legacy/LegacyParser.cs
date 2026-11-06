using System.Text.RegularExpressions;
using Chiniseapp.Application.Entries;

namespace Chiniseapp.Application.Legacy;

public sealed record LegacyArticle(string SourceId, string Word, string? Translation,
    string? WorkingSet = null, string? Comment = null, string? Transcription = null,
    string? Traditional = null)
{
    public System.Text.Json.JsonElement? SourceMetadata { get; init; }
}
public sealed record LegacyIssue(string Code, string Field, int Offset, string Fragment);
public sealed record LegacyToken(string Kind, int Offset, string Text);
public sealed record LegacyReference(string G1, string G2, string G3, string G4, string G5);
public sealed record LegacyParseResult(LegacyArticle Source, List<LegacyToken> Tokens,
    LegacyReference ReferenceMaterial, List<LegacyToken> CommentParts,
    SaveEntryContentRequest? ProposedContent, List<LegacyIssue> Issues)
{
    // A proposal is for staging/review only; this parser never changes a database.
    public bool RequiresReview => Issues.Count != 0;
}

/// <summary>
/// Conservative, lossless migration analysis based on archived article.ejs lines 155–354.
/// Offsets refer to original UTF-16 strings, including whitespace. HTML rendering heuristics
/// are evidence for review, never sufficient proof of an example or homograph boundary.
/// </summary>
public static class LegacyParser
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private static Regex Rx(string pattern) => new(pattern, RegexOptions.CultureInvariant, Timeout);
    private static readonly Regex Tokens = Rx(
        @"(?<Homograph>\{[^{}]*\}|｛[^｛｝]*｝)|(?<Roman>(?<!\S)(?:VIII|VII|III|VI|IV|IX|II|V|X|I)(?!\S))|" +
        @"(?<Pos>(?<!\S)[名动形副代量数介连助叹象缀素音](?!\S))|(?<Number>(?<!\S)\d+(?!\S))|" +
        @"(?<Style>〈[^〈〉]*〉)|(?<Domain>【[^【】]*】)|" +
        @"(?<Xr>იხ\.\s*სურ\.|იხ\.\s*ანტ\.|იგივეა,?\s+რაც|ასევე(?=\s)|იხ\.)|" +
        @"(?<Han>[\u3400-\u9fff]+)|(?<Diamond>◊)");
    private static readonly string[] Kinds = ["Homograph", "Roman", "Pos", "Number", "Style", "Domain", "Xr", "Han", "Diamond"];
    private static readonly Regex ReferenceDelimiters = Rx(@"[＊*]{2,}");
    private static readonly Regex Comments = Rx(@"(?<Note>#[^#]*#)|(?<Author>&[^&]*&)");

    public static LegacyParseResult Parse(LegacyArticle source)
    {
        var issues = new List<LegacyIssue>();
        var text = source.Translation ?? "";
        var tokens = Tokenize(text);
        var reference = ParseReference(source.WorkingSet ?? "", issues);
        var comments = ParseComments(source.Comment ?? "", issues);
        if (string.IsNullOrWhiteSpace(source.Word))
            issues.Add(new("MissingHeadword", "word", 0, source.Word));
        if (string.IsNullOrWhiteSpace(text))
            issues.Add(new("EmptyTranslation", "translation", 0, text));
        if (!string.IsNullOrWhiteSpace(source.Traditional))
            issues.Add(new("TraditionalFormMappingNeedsReview", "word", 0, source.Traditional));
        if (!string.IsNullOrWhiteSpace(source.Transcription))
            issues.Add(new("PronunciationMappingNeedsReview", "word", 0, source.Transcription));
        // A plain-definition proposal must not absorb unclassified legacy notation.
        foreach (Match match in Rx(@"[^\p{IsGeorgian}\s名动形副代量数介连助叹象缀素音,.;!?…—–\-()]+").Matches(text))
            issues.Add(new("UnclassifiedDefinitionNotation", "translation", match.Index, match.Value));

        // These cases require corpus/workbook evidence before assigning semantic boundaries.
        foreach (var token in tokens.Where(t => t.Kind is "Homograph" or "Roman" or "Xr" or "Han" or "Diamond"))
            issues.Add(new(token.Kind switch
            {
                "Homograph" => "HomographBoundaryNeedsReview",
                "Roman" => "RomanGroupNeedsReview",
                "Xr" => "CrossReferenceNeedsResolution",
                "Han" => "ChineseRunNeedsClassification",
                _ => "DiamondNeedsReview"
            }, "translation", token.Offset, token.Text));
        foreach (var token in tokens.Where(t => t.Kind is "Style" or "Domain"))
            issues.Add(new("MarkerVocabularyAndScopeNeedsReview", "translation", token.Offset, token.Text));
        foreach (var token in tokens.Where(t => t.Kind == "Number"))
            issues.Add(new("NumberBoundaryNeedsReview", "translation", token.Offset, token.Text));
        if (text.IndexOfAny(['〈', '〉', '【', '】', '{', '}', '｛', '｝']) >= 0 &&
            HasUnmatchedBrackets(text))
            issues.Add(new("UnmatchedBracket", "translation", 0, text));

        // Only simple POS + Georgian definition bodies receive semantic proposals initially.
        // All other records remain available verbatim with a machine-readable review report.
        SaveEntryContentRequest? proposal = null;
        var structuralIssues = issues.Any(i => i.Field is "translation" or "word");
        if (!structuralIssues)
        {
            var poses = tokens.Where(t => t.Kind == "Pos").ToList();
            if (poses.Count > 0 && !string.IsNullOrWhiteSpace(text[..poses[0].Offset]))
                issues.Add(new("TextBeforeFirstPos", "translation", 0, text[..poses[0].Offset]));
            else
            {
                var groups = new List<GramGroupDto>();
                for (var i = 0; i < poses.Count; i++)
                {
                    var start = poses[i].Offset + poses[i].Text.Length;
                    var end = i + 1 < poses.Count ? poses[i + 1].Offset : text.Length;
                    var definition = text[start..end].Trim();
                    if (definition.Length == 0)
                        issues.Add(new("EmptyPosDefinition", "translation", start, ""));
                    groups.Add(new GramGroupDto { Number = i + 1, Pos = poses[i].Text,
                        Senses = [new SenseDto(1, definition, [])] });
                }
                if (poses.Count == 0)
                    issues.Add(new("DefinitionWithoutPosOrStyle", "translation", 0, text));
                if (issues.All(i => i.Field is not ("translation" or "word")))
                    proposal = new(source.Word.Trim(), source.Transcription, [], 0) { GramGroups = groups };
            }
        }
        return new(source, tokens, reference, comments, proposal, issues);
    }

    public static List<LegacyParseResult> ParseBatch(IEnumerable<LegacyArticle> sources)
    {
        var results = sources.Select(Parse).ToList();
        foreach (var group in results.GroupBy(r => r.Source.Word.Trim().Replace('，', ','), StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
            foreach (var result in group)
            {
                result.Issues.Add(new("DuplicateHeadword", "word", 0,
                    string.Join(", ", group.Select(r => r.Source.SourceId))));
            }
        // Remove proposals for conflicting entries; preserve every source row in the report.
        return results.Select(r => r.Issues.Any(i => i.Code == "DuplicateHeadword")
            ? r with { ProposedContent = null } : r).ToList();
    }

    public static List<LegacyToken> Tokenize(string text)
    {
        var result = new List<LegacyToken>();
        var offset = 0;
        foreach (Match match in Tokens.Matches(text))
        {
            if (match.Index > offset) result.Add(new("Text", offset, text[offset..match.Index]));
            result.Add(new(Kinds.First(k => match.Groups[k].Success), match.Index, match.Value));
            offset = match.Index + match.Length;
        }
        if (offset < text.Length) result.Add(new("Text", offset, text[offset..]));
        return result;
    }

    private static LegacyReference ParseReference(string raw, List<LegacyIssue> issues)
    {
        var groups = new string[5];
        Array.Fill(groups, "");
        var current = 0;
        var offset = 0;
        var seen = new HashSet<int>();
        foreach (Match match in ReferenceDelimiters.Matches(raw))
        {
            groups[current] += raw[offset..match.Index];
            var length = match.Length;
            if (length > 5 || !seen.Add(length) || length <= current + 1)
            {
                issues.Add(new("InvalidReferenceDelimiter", "workingSet", match.Index, match.Value));
                groups[current] += match.Value;
            }
            else current = length - 1;
            offset = match.Index + match.Length;
        }
        groups[current] += raw[offset..];
        return new(groups[0], groups[1], groups[2], groups[3], groups[4]);
    }

    private static List<LegacyToken> ParseComments(string raw, List<LegacyIssue> issues)
    {
        var parts = new List<LegacyToken>();
        var offset = 0;
        foreach (Match match in Comments.Matches(raw))
        {
            if (match.Index > offset) parts.Add(new("Text", offset, raw[offset..match.Index]));
            parts.Add(new(match.Groups["Note"].Success ? "Note" : "Author", match.Index, match.Value));
            offset = match.Index + match.Length;
        }
        if (offset < raw.Length) parts.Add(new("Text", offset, raw[offset..]));
        foreach (var part in parts.Where(p => p.Kind == "Text" && p.Text.IndexOfAny(['#', '&']) >= 0))
            issues.Add(new("UnpairedCommentMarker", "comment", part.Offset, part.Text));
        return parts;
    }

    private static bool HasUnmatchedBrackets(string text)
    {
        var stack = new Stack<char>();
        foreach (var c in text)
        {
            if (c is '〈' or '【' or '{' or '｛') stack.Push(c);
            if (c is '〉' or '】' or '}' or '｝')
            {
                var expected = c switch { '〉' => '〈', '】' => '【', '}' => '{', _ => '｛' };
                if (!stack.TryPop(out var opening) || opening != expected) return true;
            }
        }
        return stack.Count != 0;
    }
}
