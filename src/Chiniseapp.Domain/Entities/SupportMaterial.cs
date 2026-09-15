namespace Chiniseapp.Domain.Entities;

public class SupportMaterial
{
    public int Id { get; set; }
    public int EntryId { get; set; }
    public int AuthorEditorId { get; set; }
    public string Language { get; set; } = "Other";
    public string CommentText { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
