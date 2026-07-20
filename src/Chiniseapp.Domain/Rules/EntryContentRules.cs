using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>
/// Central content-authoring permission matrix. Every active editor may view every entry;
/// these rules only gate creation and modification.
/// </summary>
public static class EntryContentRules
{
    public static bool CanCreate(EditorRole role) => role is
        EditorRole.SuperAdmin or
        EditorRole.ChiefEditor or
        EditorRole.AssistantEditor;

    public static bool CanEdit(EditorRole role, EntryStatus status) => role switch
    {
        EditorRole.SuperAdmin or EditorRole.ChiefEditor => true,
        EditorRole.AssistantEditor => status == EntryStatus.NewEntry,
        EditorRole.ZhEditor => status == EntryStatus.ZhReview,
        EditorRole.KaEditor => status == EntryStatus.KaReview,
        _ => false,
    };
}
