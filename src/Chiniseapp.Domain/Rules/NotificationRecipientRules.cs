namespace Chiniseapp.Domain.Rules;

public readonly record struct ContributorActivity(
    int EditorId, DateTime LastActivityAtUtc, int LastAuditId);

public static class NotificationRecipientRules
{
    public static IReadOnlyList<int> SelectMostRecent(
        IEnumerable<ContributorActivity> contributors, int actingEditorId, int limit = 2) =>
        contributors
            .Where(contributor => contributor.EditorId != actingEditorId)
            .GroupBy(contributor => contributor.EditorId)
            .Select(group => group
                .OrderByDescending(contributor => contributor.LastActivityAtUtc)
                .ThenByDescending(contributor => contributor.LastAuditId)
                .First())
            .OrderByDescending(contributor => contributor.LastActivityAtUtc)
            .ThenByDescending(contributor => contributor.LastAuditId)
            .Take(Math.Max(limit, 0))
            .Select(contributor => contributor.EditorId)
            .ToList();
}
