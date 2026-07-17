using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Application.Comments;

/// <summary>
/// Internal editorial notes tied to an entry (Editorial Panel spec §6) — never exposed in any
/// public/entry-content response. Always entry-scoped in Stage 1 (segment-level targeting is
/// Stage 2).
/// </summary>
public interface ICommentService
{
    Task<IReadOnlyList<CommentSummary>> GetForEntryAsync(int entryId, CancellationToken ct = default);

    /// <summary>
    /// Also awards Additional score (10.1: commenting on another author's entry counts the same
    /// as editing it) and notifies past contributors, same as a content edit.
    /// </summary>
    Task<CommentSummary> AddAsync(int entryId, CreateCommentRequest request, int authorEditorId, CancellationToken ct = default);

    /// <summary>Null if the comment doesn't exist. Throws <see cref="CommentAccessDeniedException"/> if not allowed.</summary>
    Task<CommentSummary?> UpdateTextAsync(
        int commentId, string newText, int requestingEditorId, EditorRole requestingRole, CancellationToken ct = default);

    /// <summary>False if the comment doesn't exist. Throws <see cref="CommentAccessDeniedException"/> if not allowed.</summary>
    Task<bool> ArchiveAsync(int commentId, int requestingEditorId, EditorRole requestingRole, CancellationToken ct = default);
}
