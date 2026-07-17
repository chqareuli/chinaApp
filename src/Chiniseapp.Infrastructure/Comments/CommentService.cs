using Chiniseapp.Application.Comments;
using Chiniseapp.Application.Notifications;
using Chiniseapp.Application.Scoring;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        return comments.Select(c => ToSummary(c, names)).ToList();
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

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            EntryId = entryId,
            AuthorEditorId = authorEditorId,
            CommentText = text,
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
        await scoringService.AwardForContentEditAsync(entry, authorEditorId, ct);
        await notificationService.NotifyEntryChangedAsync(entry, authorEditorId, nameof(AuditAction.CommentAdded), ct);

        await db.SaveChangesAsync(ct);

        var authorName = await db.Editors.Where(e => e.Id == authorEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToSummary(comment, new Dictionary<int, string> { [authorEditorId] = authorName });
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

        comment.CommentText = text;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var authorName = await db.Editors.Where(e => e.Id == comment.AuthorEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        return ToSummary(comment, new Dictionary<int, string> { [comment.AuthorEditorId] = authorName });
    }

    public async Task<bool> ArchiveAsync(int commentId, int requestingEditorId, EditorRole requestingRole, CancellationToken ct = default)
    {
        var comment = await db.Comments.FindAsync([commentId], ct);
        if (comment is null)
        {
            return false;
        }

        EnsureCanModify(comment, requestingEditorId, requestingRole);

        comment.Status = CommentStatus.Archived;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void EnsureCanModify(Comment comment, int requestingEditorId, EditorRole requestingRole)
    {
        var isOwner = comment.AuthorEditorId == requestingEditorId;
        var isPrivileged = requestingRole is EditorRole.SuperAdmin or EditorRole.ChiefEditor;
        if (!isOwner && !isPrivileged)
        {
            throw new CommentAccessDeniedException();
        }
    }

    private static CommentSummary ToSummary(Comment c, Dictionary<int, string> names) => new(
        c.Id, c.EntryId, c.AuthorEditorId, names.GetValueOrDefault(c.AuthorEditorId, string.Empty),
        c.CommentText, c.Status.ToString(), c.CreatedAtUtc, c.UpdatedAtUtc);
}
