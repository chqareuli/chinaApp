namespace Chiniseapp.Domain.Entities;

/// <summary>Plain internal messaging between editors, independent of any entry.</summary>
public class DirectMessage
{
    public int Id { get; set; }

    public int SenderEditorId { get; set; }

    public int RecipientEditorId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>Shared ID for per-recipient copies of an All editors message.</summary>
    public Guid? BroadcastId { get; set; }

    public DateTime SentAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}
