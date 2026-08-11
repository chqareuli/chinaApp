using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class EntryPermissionRulesTests
{
    [Theory]
    [InlineData(EditorRole.SuperAdmin, EntryStatus.Published, true)]
    [InlineData(EditorRole.ChiefEditor, EntryStatus.Published, true)]
    [InlineData(EditorRole.DeputyChiefEditor, EntryStatus.Published, true)]
    [InlineData(EditorRole.Editor, EntryStatus.Published, true)]
    [InlineData(EditorRole.KaEditor, EntryStatus.KaReview, true)]
    [InlineData(EditorRole.KaEditor, EntryStatus.ZhReview, false)]
    [InlineData(EditorRole.AssistantEditor, EntryStatus.NewEntry, true)]
    [InlineData(EditorRole.AssistantEditor, EntryStatus.ZhReview, false)]
    public void Content_editing_matches_the_document(EditorRole role, EntryStatus status, bool expected) =>
        Assert.Equal(expected, EntryPermissionRules.CanEditContent(role, status));

    [Fact]
    public void Ka_editor_can_only_view_ka_review_entries()
    {
        foreach (var status in Enum.GetValues<EntryStatus>())
        {
            Assert.Equal(status == EntryStatus.KaReview, EntryPermissionRules.CanView(EditorRole.KaEditor, status));
        }
    }

    [Theory]
    [InlineData(EditorRole.SuperAdmin, true)]
    [InlineData(EditorRole.ChiefEditor, true)]
    [InlineData(EditorRole.DeputyChiefEditor, true)]
    [InlineData(EditorRole.Editor, true)]
    [InlineData(EditorRole.KaEditor, false)]
    [InlineData(EditorRole.AssistantEditor, false)]
    public void Main_score_eligibility_matches_the_document(EditorRole role, bool expected) =>
        Assert.Equal(expected, EntryPermissionRules.CanEarnMainScore(role));
}
