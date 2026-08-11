using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>
/// Role x status transition table from SCORE.pdf. Pure and unit-tested without a DbContext,
/// since this gates every status change and must not drift silently.
/// </summary>
public static class StatusTransitionRules
{
    /// <summary>
    /// The four statuses Editor may move an entry between freely, in either direction -
    /// everything except Published and Archived.
    /// </summary>
    private static readonly EntryStatus[] EditorReachableStatuses =
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
            // Supervisor / Chief Editor / Deputy Chief Editor manage every status, including
            // publish, un-publish, and archive/problem-flag.
            EditorRole.SuperAdmin or EditorRole.ChiefEditor or EditorRole.DeputyChiefEditor => true,

            // Editor moves freely among new_entry/zh_review/ka_review/ready
            // in either direction, but never publishes, never moves a
            // published entry backward, and never archives.
            EditorRole.Editor =>
                EditorReachableStatuses.Contains(from) && EditorReachableStatuses.Contains(to),

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
