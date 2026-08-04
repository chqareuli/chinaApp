using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

public static class EditorialNoteDeletionRules
{
    public static bool CanDelete(int authorEditorId, int requestingEditorId, EditorRole requestingRole) =>
        authorEditorId == requestingEditorId ||
        requestingRole is EditorRole.ChiefEditor or EditorRole.SuperAdmin;
}
