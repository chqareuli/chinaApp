using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;

namespace Chiniseapp.Tests.Domain.Rules;

public class EditorialNoteDeletionRulesTests
{
    [Fact]
    public void Author_can_delete_own_note() =>
        Assert.True(EditorialNoteDeletionRules.CanDelete(7, 7, EditorRole.AssistantEditor));

    [Theory]
    [InlineData(EditorRole.ChiefEditor)]
    [InlineData(EditorRole.SuperAdmin)]
    public void Privileged_editor_can_delete_another_editors_note(EditorRole role) =>
        Assert.True(EditorialNoteDeletionRules.CanDelete(7, 9, role));

    [Theory]
    [InlineData(EditorRole.AssistantEditor)]
    [InlineData(EditorRole.ZhEditor)]
    [InlineData(EditorRole.KaEditor)]
    public void Other_editors_cannot_delete_another_editors_note(EditorRole role) =>
        Assert.False(EditorialNoteDeletionRules.CanDelete(7, 9, role));
}
