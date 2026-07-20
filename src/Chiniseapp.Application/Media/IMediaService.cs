namespace Chiniseapp.Application.Media;

public interface IMediaService
{
    Task<IReadOnlyList<MediaAssetItem>> ListForEntryAsync(int entryId, CancellationToken ct = default);
    Task<MediaAssetItem> UploadAsync(int entryId, UploadEntryMediaCommand command, int currentEditorId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, int currentEditorId, CancellationToken ct = default);
    Task<MediaFileContent?> GetContentAsync(string safeStorageName, CancellationToken ct = default);
}

public interface IMediaFileStorage
{
    Task SaveAsync(string safeStorageName, Stream content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string safeStorageName, CancellationToken ct = default);
    Task DeleteAsync(string safeStorageName, CancellationToken ct = default);
}

public class MediaValidationException(string message) : Exception(message);
public class MediaAccessDeniedException(string message) : Exception(message);
