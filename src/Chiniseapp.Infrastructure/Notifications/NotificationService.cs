using Chiniseapp.Application.Notifications;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Notifications;

public class NotificationService(ChiniseDbContext db) : INotificationService
{
    public async Task NotifyEntryChangedAsync(Entry entry, int actingEditorId, string changeType, CancellationToken ct = default, string? detail = null)
    {
        // Notify only the two most recent distinct previous contributors.
        // Unsaved audit rows for the change in progress are not returned by this
        // database query, and the acting editor is explicitly excluded as well.
        var recentContributors = await db.AuditLogEntries
            .Where(a =>
                a.EntityType == nameof(Entry) &&
                a.EntityId == entry.Id)
            .GroupBy(a => a.PerformedByEditorId)
            .Select(group => new
            {
                EditorId = group.Key,
                LastActivityAtUtc = group.Max(a => a.PerformedAtUtc),
                LastAuditId = group.Max(a => a.Id),
            })
            .ToListAsync(ct);

        var recipientIds = NotificationRecipientRules.SelectMostRecent(
            recentContributors.Select(contributor => new ContributorActivity(
                contributor.EditorId,
                contributor.LastActivityAtUtc,
                contributor.LastAuditId)),
            actingEditorId);

        var now = DateTime.UtcNow;
        foreach (var recipientId in recipientIds)
        {
            db.Notifications.Add(new Notification
            {
                RecipientEditorId = recipientId,
                EntryId = entry.Id,
                TriggeredByEditorId = actingEditorId,
                Type = changeType, Detail = detail,
                IsRead = false,
                CreatedAtUtc = now,
            });
        }
    }

    public async Task<IReadOnlyList<NotificationSummary>> GetForEditorAsync(int editorId, bool unreadOnly, CancellationToken ct = default)
    {
        var query = db.Notifications.Where(n => n.RecipientEditorId == editorId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var rows = await query.OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
        if (rows.Count == 0)
        {
            return [];
        }

        var entryIds = rows.Select(r => r.EntryId).Distinct().ToList();
        var editorIds = rows.Select(r => r.TriggeredByEditorId).Distinct().ToList();

        var entries = await db.Entries.Where(e => entryIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var editors = await db.Editors.Where(e => editorIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        return rows
            .Select(n => new NotificationSummary(
                n.Id, n.EntryId, entries.GetValueOrDefault(n.EntryId)?.Lemma ?? string.Empty,
                n.TriggeredByEditorId, editors.GetValueOrDefault(n.TriggeredByEditorId)?.DisplayName ?? string.Empty,
                n.Type, n.IsRead, n.CreatedAtUtc)
                {
                    Detail = n.Detail,
                    TriggeredByRole = editors.GetValueOrDefault(n.TriggeredByEditorId)?.EditorialRole ?? editors.GetValueOrDefault(n.TriggeredByEditorId)?.Role.ToString(),
                    TriggeredByIsSupervisor = editors.GetValueOrDefault(n.TriggeredByEditorId)?.Role == Chiniseapp.Domain.Enums.EditorRole.SuperAdmin,
                    EntryStatus = entries.GetValueOrDefault(n.EntryId)?.Status.ToString(),
                })
            .ToList();
    }

    public async Task<bool> MarkReadAsync(int notificationId, int requestingEditorId, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FindAsync([notificationId], ct);
        if (notification is null || notification.RecipientEditorId != requestingEditorId)
        {
            return false;
        }

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DirectMessageSummary> SendMessageAsync(int senderEditorId, SendMessageRequest request, CancellationToken ct = default)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new NotificationValidationException("Message body is required.");
        }

        if (request.AllEditors)
        {
            var recipients = await db.Editors.Where(e => e.IsActive && e.Id != senderEditorId).Select(e => e.Id).ToListAsync(ct);
            if (recipients.Count == 0) throw new NotificationValidationException("No active recipients.");
            var broadcastId = Guid.NewGuid();
            var sentAt = DateTime.UtcNow;
            var copies = recipients.Select(id => new DirectMessage { SenderEditorId = senderEditorId,
                RecipientEditorId = id, Body = body, BroadcastId = broadcastId, SentAtUtc = sentAt }).ToList();
            db.DirectMessages.AddRange(copies);
            await db.SaveChangesAsync(ct);
            return await ToSummaryAsync(copies[0], ct);
        }

        var recipientExists = await db.Editors.AnyAsync(e => e.Id == request.RecipientEditorId, ct);
        if (!recipientExists)
        {
            throw new NotificationValidationException("Recipient editor does not exist.");
        }

        var message = new DirectMessage
        {
            SenderEditorId = senderEditorId,
            RecipientEditorId = request.RecipientEditorId,
            Body = body,
            SentAtUtc = DateTime.UtcNow,
        };
        db.DirectMessages.Add(message);
        await db.SaveChangesAsync(ct);

        return await ToSummaryAsync(message, ct);
    }

    public async Task<IReadOnlyList<DirectMessageSummary>> GetInboxAsync(int editorId, CancellationToken ct = default)
    {
        var messages = await db.DirectMessages
            .Where(m => m.RecipientEditorId == editorId || m.SenderEditorId == editorId)
            .OrderByDescending(m => m.SentAtUtc)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return [];
        }

        var editorIds = messages.SelectMany(m => new[] { m.SenderEditorId, m.RecipientEditorId }).Distinct().ToList();
        var names = await db.Editors.Where(e => editorIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);

        return messages.DistinctBy(m => m.BroadcastId?.ToString() ?? $"private:{m.Id}")
            .Select(m => new DirectMessageSummary(
                m.Id,
                m.SenderEditorId, names.GetValueOrDefault(m.SenderEditorId, string.Empty),
                m.RecipientEditorId, names.GetValueOrDefault(m.RecipientEditorId, string.Empty),
                m.Body, m.SentAtUtc, m.ReadAtUtc) { BroadcastId = m.BroadcastId })
            .ToList();
    }

    public async Task<bool> MarkMessageReadAsync(int messageId, int requestingEditorId, CancellationToken ct = default)
    {
        var message = await db.DirectMessages.FindAsync([messageId], ct);
        if (message is null || message.RecipientEditorId != requestingEditorId)
        {
            return false;
        }

        message.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<DirectMessageSummary> ToSummaryAsync(DirectMessage message, CancellationToken ct)
    {
        var senderName = await db.Editors.Where(e => e.Id == message.SenderEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var recipientName = await db.Editors.Where(e => e.Id == message.RecipientEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return new DirectMessageSummary(
            message.Id, message.SenderEditorId, senderName, message.RecipientEditorId, recipientName,
            message.Body, message.SentAtUtc, message.ReadAtUtc) { BroadcastId = message.BroadcastId };
    }
}
