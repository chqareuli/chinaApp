namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Entry workflow status. Priority order for the editor main-list sort:
/// Published &gt; Ready &gt; KaReview &gt; ZhReview &gt; NewEntry. NewEntry is excluded
/// from the main list; Archived is shown separately. Only Published is visible
/// on the public site.
/// </summary>
public enum EntryStatus
{
    NewEntry,
    ZhReview,
    KaReview,
    Ready,
    Published,
    Archived,
}
