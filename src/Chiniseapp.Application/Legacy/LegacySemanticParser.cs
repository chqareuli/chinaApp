using System.Text.RegularExpressions;
using Chiniseapp.Application.Entries;

namespace Chiniseapp.Application.Legacy;

/// <summary>
/// Staging-only semantic proposals following Legacy_Parser_Source.xlsx (DB-START/END,
/// LR-START-END and ZH/KA boundaries). Unsupported forms block conversion rather than
/// being guessed. All normalization is confined to the proposal; Source is immutable.
/// </summary>
public static class LegacySemanticParser
{
    private static Regex Rx(string pattern) => new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex Markers = Rx(@"(?<Style>〈[^〈〉]+〉)|(?<Domain>【[^【】]+】)");
    private static readonly Regex Links = Rx(@"\(?(?<label>იხ\.\s*სურ\.|იხ\.\s*ანტ\.|იგივეა,?\s+რაც|იხილეთ|იხ\.|ასევე)\s+(?<body>[^():{}〈【]+)\)?");
    private static readonly Regex Chinese = Rx(@"[\u3400-\u9fff][\u3400-\u9fff，、。！？；：“”《》〈〉·…]*");
    private static readonly Regex Pos = Rx(@"(?<!\S)[名动形副代量数介连助叹象缀素音](?!\S)");
    private static readonly Regex Number = Rx(@"(?<!\S)(?<n>\d{1,2})(?!\S)");

    public static LegacyParseResult Parse(LegacyArticle source, bool complex = false)
    {
        var baseline = LegacyParser.Parse(source);
        var issues = baseline.Issues.Where(i => i.Field is "workingSet" or "comment" ||
            i.Code is "MissingHeadword" or "EmptyTranslation" or "UnmatchedBracket").ToList();
        var text = (source.Translation ?? "").Replace('\u00a0', ' ').Replace('　', ' ').Replace('：', ':');
        if (!complex && text.IndexOfAny(['{', '}', '｛', '｝', '_', '◊']) >= 0)
            issues.Add(new("UnsupportedSemanticNotation", "translation", 0, source.Translation ?? ""));
        var pinyins = Forms(source.Transcription);
        var traditional = Forms(source.Traditional);
        if (pinyins.Count > 6 || traditional.Count > 3)
            issues.Add(new("TooManyLexicalForms", "word", 0, source.Word));
        if (issues.Any(i => i.Field is "translation" or "word"))
            return baseline with { Issues = issues, ProposedContent = null };

        var groups = new List<GramGroupDto>();
        var direct = new List<SenseDto>();
        var homonyms = new List<HomonymDto>();
        var homographLabels = complex ? Rx(@"\{(?<label>[^{}]+)\}").Matches(text).Cast<Match>().ToList() : [];
        if (homographLabels.Count == 0)
            ParseScope(text, groups, direct, issues, complex);
        else
        {
            if (!string.IsNullOrWhiteSpace(text[..homographLabels[0].Index]))
                issues.Add(new("UnassignedPrefix", "translation", 0, text[..homographLabels[0].Index]));
            for (var i = 0; i < homographLabels.Count; i++)
            {
                var match = homographLabels[i];
                var label = match.Groups["label"].Value.Trim();
                var pronunciation = Rx(@"\s+\d+$").Replace(label, "");
                var order = pinyins.FindIndex(p => p.Value.Normalize() == pronunciation.Normalize());
                if (order < 0 && pronunciation == source.Word && pinyins.Count == 1) order = 0;
                var end = i + 1 < homographLabels.Count ? homographLabels[i + 1].Index : text.Length;
                var localGroups = new List<GramGroupDto>();
                var localSenses = new List<SenseDto>();
                ParseScope(text[(match.Index + match.Length)..end], localGroups, localSenses, issues, complex);
                homonyms.Add(new HomonymDto(null) { Text = label, PinyinOrder = order < 0 ? null : order,
                    LabelMode = order < 0 ? "independent" : "pinyin", GramGroups = localGroups, DirectSenses = localSenses });
                if (order < 0) issues.Add(new("HomographPronunciationNeedsReview", "translation", match.Index, label));
            }
        }
        var proposal = new SaveEntryContentRequest(source.Word.Trim(), source.Transcription, homonyms, 0)
        {
            Pinyins = pinyins, ComplexForms = traditional, GramGroups = groups, Senses = direct
        };
        issues.Add(new("StagingDraftNeedsEditorialReview", "migration", 0, source.SourceId));
        var blocking = issues.Any(i => i.Code is "UnassignedPrefix" or "UnparsedExampleText" or
            "InvalidSenseSequence" or "UnparsedChineseDefinition" or "InvalidCrossReference" or "DuplicatePosGroupsNeedReview");
        return baseline with { ProposedContent = blocking ? null : proposal, Issues = issues };
    }

    private static void ParseScope(string text, List<GramGroupDto> groups, List<SenseDto> direct,
        List<LegacyIssue> issues, bool complex)
    {
        // Roman labels precede POS, and never establish homograph identity.
        var roman = Rx(@"(?<!\S)(?:VIII|VII|III|VI|IV|IX|II|V|X|I)(?=\s+[名动形副代量数介连助叹象缀素音]\s)");
        var blocks = roman.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        foreach (var block in blocks)
        {
            var matches = Pos.Matches(block).Cast<Match>().ToList();
            if (matches.Count == 0)
            {
                direct.AddRange(ParseSenses(block, issues, complex));
                continue;
            }
            var prefix = block[..matches[0].Index];
            var prefixReferences = new List<CrossReferenceDto>();
            var prefixMarkers = new List<MarkerDto>();
            var remainingPrefix = Decorations(prefix, prefixMarkers, prefixReferences, issues, complex: complex);
            if (!string.IsNullOrWhiteSpace(remainingPrefix))
                issues.Add(new("UnassignedPrefix", "translation", 0, prefix));
            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : block.Length;
                var body = block[start..end];
                var groupMarkers = new List<MarkerDto>();
                // Leading style/domain apply to the whole POS group, before sense numbers.
                body = Rx(@"^\s*(?:(?:〈[^〈〉]+〉|【[^【】]+】)\s*)+").Replace(body, m =>
                {
                    _ = Decorations(m.Value, groupMarkers, [], issues);
                    return "";
                });
                if (i == 0) groupMarkers.InsertRange(0, prefixMarkers);
                groups.Add(new GramGroupDto
                {
                    Number = groups.Count + 1, Pos = matches[i].Value,
                    Markers = groupMarkers, CrossReferences = i == 0 ? prefixReferences : [],
                    Senses = ParseSenses(body, issues, complex)
                });
            }
        }
        if (groups.GroupBy(g => g.Pos).Any(g => g.Count() > 1))
            issues.Add(new("DuplicatePosGroupsNeedReview", "translation", 0, text));
    }

    private static List<FormItemDto> Forms(string? value) => string.IsNullOrWhiteSpace(value) ? [] :
        value.Split([',', '，', ';', '；', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select((v, i) => new FormItemDto(null, v, i)).ToList();

    private static List<SenseDto> ParseSenses(string body, List<LegacyIssue> issues, bool complex)
    {
        // Mask references and parentheses before looking for structural numbers.
        var mask = (complex ? ComplexLinks : Links).Replace(body, m => new string(' ', m.Length));
        var previous = "";
        while (previous != mask)
        {
            previous = mask;
            mask = Rx(@"\([^()]*\)|\[[^\[\]]*\]").Replace(mask, m => new string(' ', m.Length));
        }
        var numbers = Number.Matches(mask).Cast<Match>().ToList();
        var result = new List<SenseDto>();
        if (numbers.Count == 0) return [ParseSense(body, 1, issues, complex)];
        var prefix = body[..numbers[0].Index];
        var prefixMarkers = new List<MarkerDto>();
        var prefixReferences = new List<CrossReferenceDto>();
        if (!string.IsNullOrWhiteSpace(complex ? Decorations(prefix, prefixMarkers, prefixReferences, issues, complex: true) : prefix))
            issues.Add(new("UnassignedPrefix", "translation", 0, prefix));
        for (var i = 0; i < numbers.Count; i++)
        {
            if (int.Parse(numbers[i].Value) != i + 1)
                issues.Add(new("InvalidSenseSequence", "translation", 0, body));
            var start = numbers[i].Index + numbers[i].Length;
            var end = i + 1 < numbers.Count ? numbers[i + 1].Index : body.Length;
            result.Add(ParseSense(body[start..end], i + 1, issues, complex));
        }
        if (complex && result.Count > 0)
        {
            result[0].CrossReferences.InsertRange(0, prefixReferences);
            foreach (var sense in result) sense.Markers.InsertRange(0, prefixMarkers);
        }
        return result;
    }

    private static SenseDto ParseSense(string raw, int number, List<LegacyIssue> issues, bool complex)
    {
        // The workbook uses : for examples and :. for a definition with no examples.
        var colon = complex ? TopLevelColon(raw) : raw.IndexOf(':');
        var definition = colon < 0 ? raw : raw[..colon];
        var examplesRaw = colon < 0 || raw[(colon + 1)..].Trim() == "." ? "" : raw[(colon + 1)..];
        if (complex && colon < 0)
        {
            var bracketExample = Rx(@"^\s*\[[^\]]+\]\s*(?=[\u3400-\u9fff])").Match(raw);
            if (bracketExample.Success)
            {
                definition = bracketExample.Value;
                examplesRaw = raw[bracketExample.Length..];
            }
        }
        if (complex && examplesRaw.TrimStart().StartsWith("."))
            examplesRaw = examplesRaw.TrimStart()[1..];
        var markers = new List<MarkerDto>();
        var references = new List<CrossReferenceDto>();
        definition = Decorations(definition, markers, references, issues, complex ? "definition" : null, complex).Trim();
        if (!complex && Chinese.IsMatch(definition))
            issues.Add(new("UnparsedChineseDefinition", "translation", 0, raw));
        if (colon < 0) issues.Add(new("MissingDefinitionTerminator", "translation", 0, raw));
        var examples = new List<ExampleDto>();
        // Protect cross-reference targets from being mistaken for new Chinese examples.
        var maskedExamples = (complex ? ComplexLinks : Links).Replace(examplesRaw, m => new string(' ', m.Length));
        if (complex) maskedExamples = Markers.Replace(maskedExamples, m => new string(' ', m.Length));
        var starts = Chinese.Matches(maskedExamples).Cast<Match>().ToList();
        if (starts.Count == 0 && !string.IsNullOrWhiteSpace(examplesRaw))
            issues.Add(new("UnparsedExampleText", "translation", 0, examplesRaw));
        if (starts.Count > 0 && !string.IsNullOrWhiteSpace(examplesRaw[..starts[0].Index]))
            issues.Add(new("UnparsedExampleText", "translation", 0, examplesRaw[..starts[0].Index]));
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].Index : examplesRaw.Length;
            var ka = examplesRaw[(start.Index + start.Length)..end];
            var exampleMarkers = new List<MarkerDto>();
            var exampleReferences = new List<CrossReferenceDto>();
            ka = Decorations(ka, exampleMarkers, exampleReferences, issues, complex ? "ka" : null, complex).Trim();
            if (ka.Length == 0)
                issues.Add(new("IncompleteExample", "translation", 0, examplesRaw[start.Index..end]));
            examples.Add(new ExampleDto(examplesRaw.Substring(start.Index, start.Length), ka)
            { Markers = exampleMarkers, CrossReferences = exampleReferences });
        }
        return new(number, definition, examples) { Markers = markers, CrossReferences = references };
    }

    private static string Decorations(string text, List<MarkerDto> markers,
        List<CrossReferenceDto> references, List<LegacyIssue> issues, string? field = null, bool complex = false)
    {
        text = Markers.Replace(text, m =>
        {
            var kind = m.Groups["Style"].Success ? "Style" : "Domain";
            foreach (var code in m.Value[1..^1].Split([',', '，'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                markers.Add(new(null, kind, code));
            return "";
        });
        text = text.Trim();
        text = (complex ? ComplexLinks : Links).Replace(text, m =>
        {
            var label = m.Groups["label"].Value;
            var type = label.Contains("სურ") ? "illustration" : label.Contains("ანტ") ? "antonym" :
                label.StartsWith("იგივეა") ? "equivalent" : label == "ასევე" ? "seeAlso" : "see";
            var targets = new List<CrossReferenceTargetDto>();
            var display = m.Value.Trim();
            var displayCursor = 0;
            foreach (var part in m.Groups["body"].Value.Split([',', '，'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var han = Rx(@"[\u3400-\u9fff]+").Matches(part);
                if (han.Count != 1)
                {
                    issues.Add(new("InvalidCrossReference", "translation", 0, m.Value));
                    continue;
                }
                var visible = part.Trim();
                var targetOffset = display.IndexOf(visible, displayCursor, StringComparison.Ordinal);
                if (targetOffset < 0)
                {
                    issues.Add(new("InvalidCrossReference", "translation", 0, m.Value));
                    continue;
                }
                targets.Add(new(null, han[0].Value, null, null, visible, targetOffset));
                displayCursor = targetOffset + visible.Length;
            }
            if (targets.Count == 0) issues.Add(new("InvalidCrossReference", "translation", 0, m.Value));
            else references.Add(new(null, type, label, targets[0].Target, null, null,
                display, field is null ? "Attached" : "Inline")
            { Targets = targets, TextField = field, TextOffset = field is null ? null : m.Index });
            return field is null ? "" : m.Value;
        });
        return text;
    }

    private const string Target = @"[\u3400-\u9fff]+(?:\s+(?:_\d+|_[IVX]+|\d+(?=\s*[),，:.]|$)))?";
    private static readonly Regex ComplexLinks = Rx(
        @"(?:\(\s*)?(?<label>იხ\.\s*სურ\.|იხ\.\s*ანტ\.|იგივეა,?\s+რაც|იხილეთ|იხ\.|ასევე)\s+(?<body>" +
        Target + @"(?:\s*[,，]\s*" + Target + @")*)(?:\s*\))?");

    private static int TopLevelColon(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '(' or '[') depth++;
            if (text[i] is ')' or ']') depth = Math.Max(0, depth - 1);
            if (text[i] == ':' && depth == 0) return i;
        }
        return -1;
    }
}
