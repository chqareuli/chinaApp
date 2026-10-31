using Chiniseapp.Application.Entries;

namespace Chiniseapp.Infrastructure.Entries;

public static class EntryPublicationValidator
{
    public static bool HasValidReference(SenseDto sense) => sense.CrossReferences.Any(r =>
        r.TargetEntryId is > 0 || !string.IsNullOrWhiteSpace(r.Target));

    public static void Validate(SaveEntryContentRequest tree, LexicalForms forms, bool requirePinyin = true)
    {
        if (requirePinyin && (forms.Pinyins.Count is < 1 or > 6 || forms.Pinyins.Any(p => string.IsNullOrWhiteSpace(p.Value)) ||
            forms.Pinyins.Select(p => p.Value.Trim().Normalize()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != forms.Pinyins.Count))
            throw new EntryValidationException("გამოქვეყნებისთვის საჭიროა 1–6 განსხვავებული ფინინი. · Publishing requires 1–6 distinct Pinyin values.");
        var groups = tree.GramGroups.Concat(tree.Homonyms.SelectMany(h => h.GramGroups ?? [])).ToList();
        var senses = tree.Senses.Concat(tree.Homonyms.SelectMany(h => h.DirectSenses))
            .Concat(groups.SelectMany(g => g.Senses)).Concat(tree.DirectPoses.SelectMany(p => p.Senses)).ToList();
        var parentPos = new[] { tree.GramGroups.Select(g => g.Pos).Concat(tree.DirectPoses.Select(p => (string?)p.Pos)) }
            .Concat(tree.Homonyms.Select(h => (h.GramGroups ?? []).Select(g => g.Pos)));
        if (parentPos.Any(values => { var codes = values.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(); return codes.Distinct().Count() != codes.Count; }))
            throw new EntryValidationException("Merge duplicate POS groups before review · 送审前请合并重复的词性组");
        if (tree.Homonyms.Any(h => (h.GramGroups is null || h.GramGroups.Count == 0) && h.DirectSenses.Count == 0) ||
            groups.Any(g => string.IsNullOrWhiteSpace(g.Pos) || g.Senses.Count == 0) ||
            tree.DirectPoses.Any(p => string.IsNullOrWhiteSpace(p.Pos) || p.Senses.Count == 0) || senses.Count == 0)
            throw new EntryValidationException("Complete the POS and senses, then save before publishing.");
        foreach (var sense in senses)
            if (string.IsNullOrWhiteSpace(sense.Definition) && !HasValidReference(sense))
                throw new EntryValidationException($"Sense {sense.Id?.ToString() ?? sense.Number.ToString()}: provide a definition or a valid cross-reference before publishing.");
    }
}
