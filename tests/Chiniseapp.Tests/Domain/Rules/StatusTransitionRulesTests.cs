using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class StatusTransitionRulesTests
{
    private static readonly EntryStatus[] AllStatuses = Enum.GetValues<EntryStatus>();

    /// <summary>
    /// The full resolved table from docs/backend-plan.md, spelled out independently of
    /// StatusTransitionRules' implementation so this test actually locks in the spec rather
    /// than just mirroring whatever the code happens to do.
    /// </summary>
    private static readonly HashSet<(EditorRole Role, EntryStatus From, EntryStatus To)> Allowed =
        BuildAllowedSet();

    private static HashSet<(EditorRole, EntryStatus, EntryStatus)> BuildAllowedSet()
    {
        var set = new HashSet<(EditorRole, EntryStatus, EntryStatus)>();

        // Super Admin / Chief Editor: every from -> to pair.
        foreach (var role in new[] { EditorRole.SuperAdmin, EditorRole.ChiefEditor })
        {
            foreach (var from in AllStatuses)
            {
                foreach (var to in AllStatuses)
                {
                    if (from != to)
                    {
                        set.Add((role, from, to));
                    }
                }
            }
        }

        // ZH Editor: free movement among the four non-published, non-archived statuses.
        EntryStatus[] zhReachable = [EntryStatus.NewEntry, EntryStatus.ZhReview, EntryStatus.KaReview, EntryStatus.Ready];
        foreach (var from in zhReachable)
        {
            foreach (var to in zhReachable)
            {
                if (from != to)
                {
                    set.Add((EditorRole.ZhEditor, from, to));
                }
            }
        }

        // KA Editor: only the ka_review boundary, forward to ready or back to zh_review.
        set.Add((EditorRole.KaEditor, EntryStatus.KaReview, EntryStatus.ZhReview));
        set.Add((EditorRole.KaEditor, EntryStatus.KaReview, EntryStatus.Ready));

        // Assistant Editor: nothing — never changes status directly.

        return set;
    }

    public static IEnumerable<object[]> AllRoleFromToCombinations()
    {
        foreach (var role in Enum.GetValues<EditorRole>())
        {
            foreach (var from in AllStatuses)
            {
                foreach (var to in AllStatuses)
                {
                    yield return [role, from, to];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllRoleFromToCombinations))]
    public void CanTransition_matches_the_resolved_spec_table_exactly(EditorRole role, EntryStatus from, EntryStatus to)
    {
        var expected = Allowed.Contains((role, from, to));
        Assert.Equal(expected, StatusTransitionRules.CanTransition(role, from, to));
    }

    [Fact]
    public void Same_status_is_never_a_valid_transition_for_any_role()
    {
        foreach (var role in Enum.GetValues<EditorRole>())
        {
            foreach (var status in AllStatuses)
            {
                Assert.False(StatusTransitionRules.CanTransition(role, status, status));
            }
        }
    }

    [Fact]
    public void Assistant_editor_can_never_change_status()
    {
        foreach (var from in AllStatuses)
        {
            foreach (var to in AllStatuses)
            {
                if (from != to)
                {
                    Assert.False(StatusTransitionRules.CanTransition(EditorRole.AssistantEditor, from, to));
                }
            }
        }
    }

    [Theory]
    [InlineData(EntryStatus.ZhReview, EntryStatus.Published)]
    [InlineData(EntryStatus.Ready, EntryStatus.Published)]
    [InlineData(EntryStatus.KaReview, EntryStatus.Published)]
    [InlineData(EntryStatus.Published, EntryStatus.Ready)]
    [InlineData(EntryStatus.NewEntry, EntryStatus.Archived)]
    public void Zh_editor_cannot_publish_unpublish_or_archive(EntryStatus from, EntryStatus to)
    {
        Assert.False(StatusTransitionRules.CanTransition(EditorRole.ZhEditor, from, to));
    }

    [Theory]
    [InlineData(EntryStatus.NewEntry, EntryStatus.ZhReview)]
    [InlineData(EntryStatus.ZhReview, EntryStatus.KaReview)]
    [InlineData(EntryStatus.KaReview, EntryStatus.ZhReview)]
    [InlineData(EntryStatus.Ready, EntryStatus.NewEntry)]
    public void Zh_editor_moves_freely_among_the_four_working_statuses(EntryStatus from, EntryStatus to)
    {
        Assert.True(StatusTransitionRules.CanTransition(EditorRole.ZhEditor, from, to));
    }

    [Fact]
    public void Ka_editor_is_confined_to_the_ka_review_boundary()
    {
        Assert.True(StatusTransitionRules.CanTransition(EditorRole.KaEditor, EntryStatus.KaReview, EntryStatus.ZhReview));
        Assert.True(StatusTransitionRules.CanTransition(EditorRole.KaEditor, EntryStatus.KaReview, EntryStatus.Ready));
        Assert.False(StatusTransitionRules.CanTransition(EditorRole.KaEditor, EntryStatus.NewEntry, EntryStatus.ZhReview));
        Assert.False(StatusTransitionRules.CanTransition(EditorRole.KaEditor, EntryStatus.Ready, EntryStatus.Published));
    }
}
