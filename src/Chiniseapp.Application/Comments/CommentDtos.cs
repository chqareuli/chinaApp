namespace Chiniseapp.Application.Comments;
public record CommentEntryLink(int Offset, string Text, int EntryId);

public record CreateCommentRequest(string CommentText)
{
    public int? TargetSegmentId { get; init; }
    public int? ReplyToCommentId { get; init; }
}

public record CommentSummary(
    int Id, int EntryId, int AuthorEditorId, string AuthorDisplayName,
    string CommentText, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
{
    public string? AuthorRole { get; init; }
    public List<CommentEntryLink>? EntryLinks { get; init; }
    public int? TargetSegmentId { get; init; }
    public int? ReplyToCommentId { get; init; }
}
