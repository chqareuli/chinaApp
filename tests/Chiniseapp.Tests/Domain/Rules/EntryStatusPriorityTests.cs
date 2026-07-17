using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class EntryStatusPriorityTests
{
    [Theory]
    [InlineData(EntryStatus.Published, 1)]
    [InlineData(EntryStatus.Ready, 2)]
    [InlineData(EntryStatus.KaReview, 3)]
    [InlineData(EntryStatus.ZhReview, 4)]
    [InlineData(EntryStatus.NewEntry, 5)]
    [InlineData(EntryStatus.Archived, 6)]
    public void For_matches_spec_priority_order(EntryStatus status, int expectedPriority)
    {
        Assert.Equal(expectedPriority, EntryStatusPriority.For(status));
    }

    [Fact]
    public void Priorities_are_all_distinct()
    {
        var priorities = Enum.GetValues<EntryStatus>().Select(EntryStatusPriority.For).ToList();
        Assert.Equal(priorities.Count, priorities.Distinct().Count());
    }
}
