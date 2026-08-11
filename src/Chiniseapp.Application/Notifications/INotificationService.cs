using Chiniseapp.Domain.Entities;

namespace Chiniseapp.Application.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Stages Notification rows for the two most recent distinct previous contributors,
    /// excluding the editor who made the current change. Caller's SaveChangesAsync persists
    /// them alongside whatever triggered them.
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
