namespace Chiniseapp.Domain.Entities;

/// <summary>
/// One internal supporting-material note attached to an entry. The author is
/// retained for auditing, but is intentionally not exposed in the list DTO.
/// </summary>
public class EntrySupportMaterial
{
    public int Id { get; set; }

    public int EntryId { get; set; }

    public Entry Entry { get; set; } = null!;

    public int AddedByEditorId { get; set; }

    public Editor AddedByEditor { get; set; } = null!;

    public string Language { get; set; } = string.Empty;

    public string CommentText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
