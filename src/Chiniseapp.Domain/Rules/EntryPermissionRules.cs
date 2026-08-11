using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>Role and status permissions defined by SCORE.pdf.</summary>
public static class EntryPermissionRules
{
    public static bool CanCreate(EditorRole role) => role is
        EditorRole.SuperAdmin or
        EditorRole.ChiefEditor or
        EditorRole.DeputyChiefEditor or
        EditorRole.Editor or
        EditorRole.AssistantEditor;

    public static bool CanView(EditorRole role, EntryStatus status) =>
        role != EditorRole.KaEditor || status == EntryStatus.KaReview;

    public static bool CanEditContent(EditorRole role, EntryStatus status) => role switch
    {
        EditorRole.SuperAdmin or EditorRole.ChiefEditor or EditorRole.DeputyChiefEditor or EditorRole.Editor => true,
        EditorRole.KaEditor => status == EntryStatus.KaReview,
        EditorRole.AssistantEditor => status == EntryStatus.NewEntry,
        _ => false,
    };

    public static bool CanComment(EditorRole role, EntryStatus status) => CanEditContent(role, status);

    public static bool CanEarnMainScore(EditorRole role) => role is
        EditorRole.SuperAdmin or
        EditorRole.ChiefEditor or
        EditorRole.DeputyChiefEditor or
        EditorRole.Editor;
}
