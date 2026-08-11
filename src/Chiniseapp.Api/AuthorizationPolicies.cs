using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Api;

public static class AuthorizationPolicies
{
    /// <summary>Editors allowed to create/edit new_entry content, per the 11.1 permissions matrix.</summary>
    public const string ContentEditorRoles =
        $"{nameof(EditorRole.SuperAdmin)},{nameof(EditorRole.ChiefEditor)},{nameof(EditorRole.DeputyChiefEditor)}," +
        $"{nameof(EditorRole.Editor)},{nameof(EditorRole.KaEditor)},{nameof(EditorRole.AssistantEditor)}";

    public const string EntryCreatorRoles =
        $"{nameof(EditorRole.SuperAdmin)},{nameof(EditorRole.ChiefEditor)},{nameof(EditorRole.DeputyChiefEditor)}," +
        $"{nameof(EditorRole.Editor)},{nameof(EditorRole.AssistantEditor)}";
}
