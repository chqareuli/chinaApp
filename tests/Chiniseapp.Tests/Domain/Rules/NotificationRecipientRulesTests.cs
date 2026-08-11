using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class NotificationRecipientRulesTests
{
    [Fact]
    public void Selects_only_the_two_most_recent_distinct_previous_editors()
    {
        var now = DateTime.UtcNow;
        ContributorActivity[] contributors =
        [
            new(1, now.AddMinutes(-1), 10),
            new(2, now.AddMinutes(-2), 9),
            new(3, now.AddMinutes(-3), 8),
            new(2, now.AddMinutes(-5), 4),
        ];

        var recipients = NotificationRecipientRules.SelectMostRecent(contributors, actingEditorId: 4);

        Assert.Equal([1, 2], recipients);
    }

    [Fact]
    public void Excludes_the_actor_before_selecting_the_last_two()
    {
        var now = DateTime.UtcNow;
        ContributorActivity[] contributors =
        [
            new(9, now, 12),
            new(1, now.AddMinutes(-1), 11),
            new(2, now.AddMinutes(-2), 10),
            new(3, now.AddMinutes(-3), 9),
        ];

        var recipients = NotificationRecipientRules.SelectMostRecent(contributors, actingEditorId: 9);

        Assert.Equal([1, 2], recipients);
    }
}
