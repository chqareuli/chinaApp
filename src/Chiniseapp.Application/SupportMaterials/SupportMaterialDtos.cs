namespace Chiniseapp.Application.SupportMaterials;

public sealed record CreateSupportMaterialRequest(string Language, string CommentText);

public sealed record SupportMaterialSummary(
    int Id,
    int EntryId,
    string Language,
    string CommentText,
    DateTime CreatedAtUtc,
    bool CanDelete);
