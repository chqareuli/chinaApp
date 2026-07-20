using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class EntryContentRulesTests
{
    [Theory]
    [InlineData(EditorRole.SuperAdmin, true)]
    [InlineData(EditorRole.ChiefEditor, true)]
    [InlineData(EditorRole.ZhEditor, false)]
    [InlineData(EditorRole.KaEditor, false)]
    [InlineData(EditorRole.AssistantEditor, true)]
    public void CanCreate_matches_the_product_role_matrix(EditorRole role, bool expected)
    {
        Assert.Equal(expected, EntryContentRules.CanCreate(role));
    }

    public static IEnumerable<object[]> AllRoleStatusCombinations()
    {
        foreach (var role in Enum.GetValues<EditorRole>())
        {
            foreach (var status in Enum.GetValues<EntryStatus>())
            {
                yield return [role, status];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllRoleStatusCombinations))]
    public void CanEdit_matches_the_product_role_and_status_matrix(EditorRole role, EntryStatus status)
    {
        var expected = role switch
        {
            EditorRole.SuperAdmin or EditorRole.ChiefEditor => true,
            EditorRole.AssistantEditor => status == EntryStatus.NewEntry,
            EditorRole.ZhEditor => status == EntryStatus.ZhReview,
            EditorRole.KaEditor => status == EntryStatus.KaReview,
            _ => false,
        };

        Assert.Equal(expected, EntryContentRules.CanEdit(role, status));
    }
}
