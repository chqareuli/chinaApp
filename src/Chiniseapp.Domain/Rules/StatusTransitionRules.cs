using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>
/// Role x status transition table, resolved in docs/backend-plan.md ("Status workflow"): the
/// broad ZH-editor model from the detailed source spec, not the narrower forward-only model a
/// later shorter spec proposed. Pure and unit-tested without a DbContext, since this gates every
/// status change and must not drift silently.
/// </summary>
public static class StatusTransitionRules
{
    /// <summary>
    /// The four statuses ZH Editor may move an entry between freely, in either direction —
    /// everything except Published and Archived.
    /// </summary>
    private static readonly EntryStatus[] ZhEditorReachableStatuses =
    [
        EntryStatus.NewEntry,
        EntryStatus.ZhReview,
        EntryStatus.KaReview,
        EntryStatus.Ready,
    ];

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

            // ZH Editor moves freely among new_entry/zh_review/ka_review/ready
            // in either direction, but never publishes, never moves a
            // published entry backward, and never archives.
            EditorRole.ZhEditor =>
                ZhEditorReachableStatuses.Contains(from) && ZhEditorReachableStatuses.Contains(to),

            // KA Editor only works the ka_review boundary: forward to ready,
            // or back to zh_review.
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
