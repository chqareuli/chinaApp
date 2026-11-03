using Chiniseapp.Application.Entries;

namespace Chiniseapp.Infrastructure.Entries;

public static class CrossReferenceRules
{
    public static string Text(CrossReferenceDto reference) => reference.DisplayText ?? reference.Label ?? "";
    public static bool HasTarget(CrossReferenceDto reference) => reference.Targets is { Count: > 0 }
        ? reference.Targets.All(t => t.TargetEntryId is > 0 || !string.IsNullOrWhiteSpace(t.Target))
        : reference.TargetEntryId is > 0 || !string.IsNullOrWhiteSpace(reference.Target);

    public static void ValidateTargets(CrossReferenceDto reference)
    {
        var text = Text(reference);
        var cursor = 0;
        var ids = new HashSet<string>();
        foreach (var target in reference.Targets ?? [])
        {
            if (target.TargetEntryId is <= 0 || string.IsNullOrWhiteSpace(target.Target) && target.TargetEntryId is not > 0 ||
                string.IsNullOrEmpty(target.DisplayText) || target.TextOffset < cursor || target.TextOffset > text.Length - target.DisplayText.Length ||
                !text.AsSpan(target.TextOffset, target.DisplayText.Length).SequenceEqual(target.DisplayText.AsSpan()) ||
                target.Id is not null && !ids.Add(target.Id))
                throw new EntryValidationException($"Cross-reference {reference.Id}: invalid, duplicate or overlapping target. Preserve its original text and review the target list.");
            cursor = target.TextOffset + target.DisplayText.Length;
        }
    }

    public static CrossReferenceDto AssignTargetIdentities(CrossReferenceDto reference, CrossReferenceDto? previous)
    {
        reference = reference with { Targets = reference.Targets ?? [] };
        if (previous?.Targets?.Count > 0 && reference.Targets.Count == 0)
            throw new EntryValidationException("This link has a target list. Reload the current editor before changing it. · 此链接包含目标列表，请重新加载编辑器后再修改。");
        ValidateTargets(reference);
        var owned = (previous?.Targets ?? []).Select(t => t.Id).ToHashSet();
        var targets = reference.Targets.Select(t =>
        {
            if (t.Id is not null && !owned.Contains(t.Id))
                throw new EntryValidationException($"Cross-reference {reference.Id}: foreign target identity. Reload the entry.");
            return t with { Id = t.Id ?? Guid.NewGuid().ToString("N") };
        }).ToList();
        return reference with { Targets = targets,
            Target = targets.FirstOrDefault()?.Target ?? reference.Target,
            TargetEntryId = targets.FirstOrDefault()?.TargetEntryId ?? (targets.Count == 0 ? reference.TargetEntryId : null),
            TargetEntryLemma = targets.FirstOrDefault()?.TargetEntryLemma ?? (targets.Count == 0 ? reference.TargetEntryLemma : null) };
    }
}
