using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Application.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Stages a Notification row (per §7 of the spec) for every editor who has ever
    /// contributed to this entry — its creator, its main_author (once locked), and
    /// everyone with a prior AuditLogEntry against it — except whoever just made this
    /// change (resolved Q4: no self-notifications). Caller's SaveChangesAsync persists
    /// these alongside whatever triggered them.
    /// </summary>
    Task NotifyEntryChangedAsync(Entry entry, int actingEditorId, string changeType, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationSummary>> GetForEditorAsync(int editorId, bool unreadOnly, CancellationToken ct = default);

    /// <summary>Returns false if the notification doesn't exist or isn't addressed to requestingEditorId.</summary>
    Task<bool> MarkReadAsync(int notificationId, int requestingEditorId, CancellationToken ct = default);

    Task<DirectMessageSummary> SendMessageAsync(int senderEditorId, SendMessageRequest request, CancellationToken ct = default);

    /// <summary>Messages where the editor is either sender or recipient, newest first.</summary>
    Task<IReadOnlyList<DirectMessageSummary>> GetInboxAsync(int editorId, CancellationToken ct = default);

    /// <summary>Returns false if the message doesn't exist or isn't addressed to requestingEditorId.</summary>
    Task<bool> MarkMessageReadAsync(int messageId, int requestingEditorId, CancellationToken ct = default);
}
