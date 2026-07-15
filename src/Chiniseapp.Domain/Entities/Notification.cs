namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Entry-linked notification: sent to main_author, last_editor, and everyone
/// who ever edited/commented on the entry, excluding whoever just made the
/// change. Must link back to the entry (Editorial Panel spec §7).
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public int RecipientEditorId { get; set; }

    public int EntryId { get; set; }

    public int TriggeredByEditorId { get; set; }

    /// <summary>Short machine-readable reason, e.g. "content_edited", "status_changed".</summary>
    public string Type { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
