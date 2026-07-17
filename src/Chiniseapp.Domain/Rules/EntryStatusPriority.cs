using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Domain.Rules;

/// <summary>
/// The editor main-list sort key (5.1 Main editor page list): Published sorts
/// first, NewEntry second-to-last (and is excluded from that list entirely),
/// Archived last. Kept as one pure, testable mapping rather than a DB-computed
/// column, per the backend plan.
/// </summary>
public static class EntryStatusPriority
{
    public static int For(EntryStatus status) => status switch
    {
        EntryStatus.Published => 1,
        EntryStatus.Ready => 2,
        EntryStatus.KaReview => 3,
        EntryStatus.ZhReview => 4,
        EntryStatus.NewEntry => 5,
        EntryStatus.Archived => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown entry status."),
    };
}
