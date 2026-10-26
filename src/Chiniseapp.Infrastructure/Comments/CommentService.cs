using Chiniseapp.Application.Comments;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chiniseapp.Infrastructure.Comments;

public class CommentService(ChiniseDbContext db, IScoringService scoringService, INotificationService notificationService) : ICommentService
{
    public async Task<IReadOnlyList<CommentSummary>> GetForEntryAsync(int entryId, CancellationToken ct = default)
    {
        var comments = await db.Comments
            .Where(c => c.EntryId == entryId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        if (comments.Count == 0)
        {
            return [];
        }

        var authorIds = comments.Select(c => c.AuthorEditorId).Distinct().ToList();
        var names = await db.Editors.Where(e => authorIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);

        var roles = await db.Editors.Where(e => authorIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Role, ct);
        return comments.Select(c => ToSummary(c, names) with { AuthorRole = roles.GetValueOrDefault(c.AuthorEditorId).ToString() }).ToList();
    }

    public async Task<CommentSummary> AddAsync(int entryId, CreateCommentRequest request, int authorEditorId, CancellationToken ct = default)
    {
        var text = request.CommentText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new CommentValidationException("Comment text is required.");
        }

        var entry = await db.Entries.FindAsync([entryId], ct)
            ?? throw new CommentValidationException($"Entry {entryId} does not exist.");
        var actor = await db.Editors.FindAsync([authorEditorId], ct)
            ?? throw new CommentValidationException("Current editor does not exist.");
        if (!EntryPermissionRules.CanComment(actor.Role, entry.Status))
        {
            throw new CommentAccessDeniedException();
        }

        var now = DateTime.UtcNow;
        entry.UpdatedAtUtc = now;
        var targetSegmentId = request.TargetSegmentId;
        int? replyRecipientId = null;
        if (request.ReplyToCommentId is { } replyId)
        {
            var parent = await db.Comments.SingleOrDefaultAsync(c => c.Id == replyId && c.EntryId == entryId, ct)
                ?? throw new CommentValidationException("Reply must refer to a comment on this entry.");
            targetSegmentId = parent.TargetSegmentId;
            replyRecipientId = parent.AuthorEditorId;
        }
        if (targetSegmentId is { } segmentId && !await db.Segments.AnyAsync(s => s.Id == segmentId && s.EntryId == entryId, ct))
            throw new CommentValidationException("Comment target must belong to this entry.");
        var comment = new Comment
        {
            EntryId = entryId,
            TargetSegmentId = targetSegmentId,
            ReplyToCommentId = request.ReplyToCommentId,
            AuthorEditorId = authorEditorId,
            CommentText = text,
            EntryLinks = await ResolveEntryLinks(text, actor.Role, ct),
            Status = CommentStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Comments.Add(comment);

        db.AuditLogEntries.Add(new AuditLogEntry
        {
            EntityType = nameof(Entry),
            EntityId = entryId,
            Action = AuditAction.CommentAdded,
            PerformedByEditorId = authorEditorId,
            PerformedAtUtc = now,
        });

        // 10.1: commenting on another author's entry earns Additional score,
        // exactly like editing its content does.
        await scoringService.AwardForContentEditAsync(entry, actor.Role, authorEditorId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, authorEditorId, nameof(AuditAction.CommentAdded), ct, comment.CommentText);
        if (replyRecipientId is int recipientId && recipientId != authorEditorId
            && await db.Editors.AnyAsync(e => e.Id == recipientId && e.IsActive && e.Role != EditorRole.KaEditor, ct)
            && !db.Notifications.Local.Any(n => n.EntryId == entryId && n.RecipientEditorId == recipientId && n.TriggeredByEditorId == authorEditorId && n.CreatedAtUtc >= now))
        {
            db.Notifications.Add(new Notification { EntryId = entryId, RecipientEditorId = recipientId,
                TriggeredByEditorId = authorEditorId, Type = nameof(AuditAction.CommentAdded), Detail = text, CreatedAtUtc = now });
        }

        await db.SaveChangesAsync(ct);

        var authorName = await db.Editors.Where(e => e.Id == authorEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToSummary(comment, new Dictionary<int, string> { [authorEditorId] = authorName }) with { AuthorRole = actor.Role.ToString() };
    }

    public async Task<CommentSummary?> UpdateTextAsync(
        int commentId, string newText, int requestingEditorId, EditorRole requestingRole, CancellationToken ct = default)
    {
        var comment = await db.Comments.FindAsync([commentId], ct);
        if (comment is null)
        {
            return null;
        }

        EnsureCanModify(comment, requestingEditorId, requestingRole);

        var text = newText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new CommentValidationException("Comment text is required.");
        }

        var previousText = comment.CommentText;
        comment.CommentText = text;
        comment.EntryLinks = await ResolveEntryLinks(text, requestingRole, ct, comment.EntryLinks);
        comment.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogEntries.Add(new AuditLogEntry { EntityType = nameof(Entry), EntityId = comment.EntryId,
            PerformedByEditorId = requestingEditorId, PerformedAtUtc = comment.UpdatedAtUtc, Action = AuditAction.ContentEdited,
            Notes = "Comment updated · 评论已更新", OldValue = JsonSerializer.Serialize(new { comment.Id, text = previousText }),
            NewValue = JsonSerializer.Serialize(new { comment.Id, text }) });
        var entry = await db.Entries.SingleAsync(e => e.Id == comment.EntryId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, requestingEditorId, nameof(AuditAction.ContentEdited), ct, "Comment updated · 评论已更新");
        await db.SaveChangesAsync(ct);

        var authorName = await db.Editors.Where(e => e.Id == comment.AuthorEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var authorRole = await db.Editors.Where(e => e.Id == comment.AuthorEditorId).Select(e => e.Role).SingleAsync(ct);
        return ToSummary(comment, new Dictionary<int, string> { [comment.AuthorEditorId] = authorName }) with { AuthorRole = authorRole.ToString() };
    }

    public async Task<bool> ArchiveAsync(int commentId, int requestingEditorId, EditorRole requestingRole, CancellationToken ct = default)
    {
        var comment = await db.Comments.FindAsync([commentId], ct);
        if (comment is null)
        {
            return false;
        }

        EnsureCanModify(comment, requestingEditorId, requestingRole);

        var previousStatus = comment.Status.ToString();
        comment.Status = CommentStatus.Archived;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogEntries.Add(new AuditLogEntry { EntityType = nameof(Entry), EntityId = comment.EntryId,
            PerformedByEditorId = requestingEditorId, PerformedAtUtc = comment.UpdatedAtUtc, Action = AuditAction.ContentEdited,
            Notes = "Comment archived · 评论已归档", OldValue = JsonSerializer.Serialize(new { comment.Id, status = previousStatus }),
            NewValue = JsonSerializer.Serialize(new { comment.Id, status = "Archived" }) });
        var entry = await db.Entries.SingleAsync(e => e.Id == comment.EntryId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, requestingEditorId, nameof(AuditAction.ContentEdited), ct, "Comment archived · 评论已归档");
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void EnsureCanModify(Comment comment, int requestingEditorId, EditorRole requestingRole)
    {
        var isOwner = comment.AuthorEditorId == requestingEditorId;
        var isPrivileged = requestingRole is
            EditorRole.SuperAdmin or EditorRole.ChiefEditor or EditorRole.DeputyChiefEditor;
        if (!isOwner && !isPrivileged)
        {
            throw new CommentAccessDeniedException();
        }
    }

    private static CommentSummary ToSummary(Comment c, Dictionary<int, string> names) => new(
        c.Id, c.EntryId, c.AuthorEditorId, names.GetValueOrDefault(c.AuthorEditorId, string.Empty),
        c.CommentText, c.Status.ToString(), c.CreatedAtUtc, c.UpdatedAtUtc)
        { TargetSegmentId = c.TargetSegmentId, ReplyToCommentId = c.ReplyToCommentId,
          EntryLinks = c.EntryLinks is null ? null : JsonSerializer.Deserialize<List<CommentEntryLink>>(c.EntryLinks) };

    private async Task<string> ResolveEntryLinks(string text, EditorRole role, CancellationToken ct, string? previousLinks = null)
    {
        var matches = Regex.Matches(text, "[\\u3400-\\u9fff]+").Cast<Match>().ToList();
        var titles = matches.Select(m => m.Value).Distinct().ToList();
        var candidates = await db.Entries.AsNoTracking().Where(e => titles.Contains(e.Lemma)).ToListAsync(ct);
        var unique = candidates.Where(e => EntryPermissionRules.CanView(role, e.Status)).GroupBy(e => e.Lemma)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Id);
        if (previousLinks is not null)
        {
            var links = JsonSerializer.Deserialize<List<CommentEntryLink>>(previousLinks) ?? [];
            var ids = links.Select(link => link.EntryId).Distinct().ToList();
            var surviving = await db.Entries.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct);
            foreach (var link in links.Where(link => titles.Contains(link.Text)))
                if (surviving.Any(e => e.Id == link.EntryId && EntryPermissionRules.CanView(role, e.Status))) unique[link.Text] = link.EntryId;
        }
        return JsonSerializer.Serialize(matches.Where(m => unique.ContainsKey(m.Value))
            .Select(m => new CommentEntryLink(m.Index, m.Value, unique[m.Value])).ToList());
    }
}
