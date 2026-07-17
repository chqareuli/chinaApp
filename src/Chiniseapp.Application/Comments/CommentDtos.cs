namespace Chiniseapp.Application.Comments;

public record CreateCommentRequest(string CommentText);

public record CommentSummary(
    int Id, int EntryId, int AuthorEditorId, string AuthorDisplayName,
    string CommentText, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
