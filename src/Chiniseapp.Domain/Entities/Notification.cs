namespace Chiniseapp.Domain.Entities;

/// <summary>
/// Entry-linked notification sent to the two most recent distinct previous
/// contributors, excluding whoever just made the change.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public int RecipientEditorId { get; set; }

    public int EntryId { get; set; }

    public int TriggeredByEditorId { get; set; }

    /// <summary>Short machine-readable reason, e.g. "content_edited", "status_changed".</summary>
    public string Type { get; set; } = string.Empty;

    public string? Detail { get; set; }
    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
