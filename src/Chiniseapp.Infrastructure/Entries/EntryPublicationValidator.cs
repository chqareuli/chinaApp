using Chiniseapp.Application.Entries;

namespace Chiniseapp.Infrastructure.Entries;

public static class EntryPublicationValidator
{
    public static bool HasValidReference(SenseDto sense) => sense.CrossReferences.Any(CrossReferenceRules.HasTarget);

    public static void Validate(SaveEntryContentRequest tree, LexicalForms forms, bool requirePinyin = true)
    {
        if (requirePinyin && forms.Pinyins.Count == 0)
            throw new EntryValidationException("At least one Pinyin pronunciation is required for publication. · 发布前须至少填写一项拼音。");
        if (requirePinyin && (forms.Pinyins.Count > 6 || forms.Pinyins.Any(p => string.IsNullOrWhiteSpace(p.Value)) ||
            forms.Pinyins.Select(p => p.Value.Trim().Normalize()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != forms.Pinyins.Count))
            throw new EntryValidationException("Pinyin values must be nonempty, distinct and limited to six. · 拼音须非空且唯一，最多六项。");
        var groups = tree.GramGroups.Concat(tree.Homonyms.SelectMany(h => h.GramGroups ?? [])).ToList();
        var senses = tree.Senses.Concat(tree.Homonyms.SelectMany(h => h.DirectSenses))
            .Concat(groups.SelectMany(g => g.Senses)).Concat(tree.DirectPoses.SelectMany(p => p.Senses)).ToList();
        var parentPos = new[] { tree.GramGroups.Select(g => g.Pos).Concat(tree.DirectPoses.Select(p => (string?)p.Pos)) }
            .Concat(tree.Homonyms.Select(h => (h.GramGroups ?? []).Select(g => g.Pos)));
        if (parentPos.Any(values => { var codes = values.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(); return codes.Distinct().Count() != codes.Count; }))
            throw new EntryValidationException("Merge duplicate POS groups before review · 送审前请合并重复的词性组");
        if (groups.Any(g => string.IsNullOrWhiteSpace(g.Pos)) || tree.DirectPoses.Any(p => string.IsNullOrWhiteSpace(p.Pos)))
            throw new EntryValidationException("Every grammatical group must have a selected part of speech (POS) before publication. · 发布前须为每个语法组选择词性（POS）。");
        if (tree.Homonyms.Any(h => (h.GramGroups is null || h.GramGroups.Count == 0) && h.DirectSenses.Count == 0) ||
            groups.Any(g => g.Senses.Count == 0) ||
            tree.DirectPoses.Any(p => string.IsNullOrWhiteSpace(p.Pos) || p.Senses.Count == 0) || senses.Count == 0)
            throw new EntryValidationException("Complete the grammatical groups and senses before publication. · 发布前请补全语法组与义项。");
        foreach (var sense in senses)
            if (string.IsNullOrWhiteSpace(sense.Definition) && !HasValidReference(sense))
                throw new EntryValidationException($"Sense {sense.Id?.ToString() ?? sense.Number.ToString()}: Every sense must have a definition or a valid cross-reference before publication. · 发布前，每个义项须有释义或有效的交叉引用。");
    }
}
