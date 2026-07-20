namespace Chiniseapp.Application.Notifications;

public record NotificationSummary(
    int Id, int EntryId, string EntryLemma,
    int TriggeredByEditorId, string TriggeredByDisplayName,
    string Type, bool IsRead, DateTime CreatedAtUtc);

public record SendMessageRequest(int RecipientEditorId, string Body);

public record UnreadActivitySummary(int Notifications, int Messages);

public record DirectMessageSummary(
    int Id,
    int SenderEditorId, string SenderDisplayName,
    int RecipientEditorId, string RecipientDisplayName,
    string Body, DateTime SentAtUtc, DateTime? ReadAtUtc);
