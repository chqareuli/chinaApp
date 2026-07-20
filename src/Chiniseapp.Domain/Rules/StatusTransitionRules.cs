using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>
/// Role x status transition table from the current product requirements. Pure and unit-tested
/// without a DbContext, since this gates every status change and must not drift silently.
/// </summary>
public static class StatusTransitionRules
{
    public static bool CanTransition(EditorRole role, EntryStatus from, EntryStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return role switch
        {
            // Chief Editor / Super Admin manage every status, including
            // publish, un-publish, and archive/problem-flag.
            EditorRole.SuperAdmin or EditorRole.ChiefEditor => true,

            // The Chinese editor accepts a new entry into Chinese review and,
            // after checking it, sends it to Georgian review.
            EditorRole.ZhEditor =>
                (from == EntryStatus.NewEntry && to == EntryStatus.ZhReview) ||
                (from == EntryStatus.ZhReview && to == EntryStatus.KaReview),

            // The Georgian editor either completes Georgian review or sends
            // the entry back to Chinese review for correction.
            EditorRole.KaEditor =>
                from == EntryStatus.KaReview && (to == EntryStatus.ZhReview || to == EntryStatus.Ready),

            // Assistant Editor never changes status directly (their entries
            // are promoted by a supervisor, who becomes the actor for that
            // transition — main_author locking is a separate concern, see
            // EntryService.ChangeStatusAsync).
            EditorRole.AssistantEditor => false,

            _ => false,
        };
    }
}
