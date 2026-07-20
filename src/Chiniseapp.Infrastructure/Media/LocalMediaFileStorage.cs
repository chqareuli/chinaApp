using Chiniseapp.Application.Media;
using Microsoft.Extensions.Configuration;

namespace Chiniseapp.Infrastructure.Media;

public class LocalMediaFileStorage : IMediaFileStorage
{
    private readonly string rootPath;

    public LocalMediaFileStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["MediaStorage:RootPath"];
        rootPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "storage", "media")
                : configuredPath);
        Directory.CreateDirectory(rootPath);
    }

    public async Task SaveAsync(string safeStorageName, Stream content, CancellationToken ct = default)
    {
        var path = Resolve(safeStorageName);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(output, ct);
    }

    public Task<Stream?> OpenReadAsync(string safeStorageName, CancellationToken ct = default)
    {
        var path = Resolve(safeStorageName);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string safeStorageName, CancellationToken ct = default)
    {
        var path = Resolve(safeStorageName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string safeStorageName)
    {
        var filename = Path.GetFileName(safeStorageName);
        if (!string.Equals(filename, safeStorageName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid media storage name.");
        }

        var resolved = Path.GetFullPath(Path.Combine(rootPath, filename));
        if (!resolved.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Media path escaped the configured storage root.");
        }

        return resolved;
    }
}
