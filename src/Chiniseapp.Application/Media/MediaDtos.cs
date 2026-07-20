namespace Chiniseapp.Application.Media;

public record MediaAssetItem(
    int Id,
    int EntryId,
    string OriginalFilename,
    string MimeType,
    string StorageUrl,
    string? ThumbnailUrl,
    string? Placement,
    int UploadedByEditorId,
    string UploadedByDisplayName,
    DateTime UploadedAtUtc);

public record UploadEntryMediaCommand(
    Stream Content,
    string OriginalFilename,
    string MimeType,
    long Length,
    string? Placement);

public record MediaFileContent(Stream Content, string MimeType, string OriginalFilename);
