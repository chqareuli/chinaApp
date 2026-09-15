namespace Chiniseapp.Application.Notifications;

public record NotificationSummary(
    int Id, int EntryId, string EntryLemma,
    int TriggeredByEditorId, string TriggeredByDisplayName,
    string Type, bool IsRead, DateTime CreatedAtUtc)
{
    public string? TriggeredByRole { get; init; }
    public bool TriggeredByIsSupervisor { get; init; }
    public string? Detail { get; init; }
    public string? EntryStatus { get; init; }
}

public record SendMessageRequest(int RecipientEditorId, string Body)
{
    public bool AllEditors { get; init; }
}

public record DirectMessageSummary(
    int Id,
    int SenderEditorId, string SenderDisplayName,
    int RecipientEditorId, string RecipientDisplayName,
    string Body, DateTime SentAtUtc, DateTime? ReadAtUtc)
{
    public Guid? BroadcastId { get; init; }
}
