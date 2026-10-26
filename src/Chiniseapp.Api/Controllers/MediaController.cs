using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chiniseapp.Api.Controllers;

[ApiController, Authorize]
public class MediaController(ChiniseDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    private string StorageDirectory => Path.Combine(environment.ContentRootPath, "App_Data", "media");
    private async Task<object> Summary(MediaAsset m, CancellationToken ct) => new {
        m.Id, m.EntryId, m.OriginalFilename, DisplayName = m.DisplayName ?? Path.GetFileNameWithoutExtension(m.OriginalFilename), m.MimeType, m.StorageUrl, m.ThumbnailUrl, m.Placement,
        m.UploadedByEditorId, UploadedByDisplayName = await db.Editors.Where(e => e.Id == m.UploadedByEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct), m.UploadedAtUtc,
        LinkedEntries = await LinkedEntries(m.SafeStorageName, ct), CanManage = await CanManage(m.SafeStorageName, ct)
    };
    private async Task<object[]> LinkedEntries(string key, CancellationToken ct)
    {
        var entries = await db.Entries.AsNoTracking().Where(e => db.MediaAssets.Any(m => m.SafeStorageName == key && m.EntryId == e.Id)).ToListAsync(ct);
        return entries.Where(e => EntryPermissionRules.CanView(User.GetEditorRole(), e.Status))
            .Select(e => (object)new { e.Id, e.Lemma, Status = e.Status.ToString() }).ToArray();
    }
    private async Task<bool> CanManage(string key, CancellationToken ct)
    {
        var statuses = await db.Entries.Where(e => db.MediaAssets.Any(m => m.SafeStorageName == key && m.EntryId == e.Id)).Select(e => e.Status).ToListAsync(ct);
        return statuses.Count > 0 && statuses.All(status => EntryPermissionRules.CanEditContent(User.GetEditorRole(), status));
    }

    public record RenameMediaRequest(string DisplayName);
    [HttpPut("media/{id:int}/name")]
    public async Task<IActionResult> Rename(int id, RenameMediaRequest request, CancellationToken ct)
    {
        var item = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 200)
            return BadRequest(new { message = "Enter an image name (1–200 characters). · 请输入图片名称。" });
        var name = request.DisplayName.Trim().ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var nameLock = "media-name:" + name;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({nameLock}, 0))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({item.SafeStorageName}, 0))", ct);
        if (!await CanManage(item.SafeStorageName, ct)) return Forbid();
        if (await db.MediaAssets.AnyAsync(m => m.SafeStorageName != item.SafeStorageName && m.DisplayName != null && m.DisplayName.Trim().ToLower() == name, ct))
            return Conflict(new { message = "სახელი უკვე გამოყენებულია. · 图片名称已被使用。" });
        await db.MediaAssets.Where(m => m.SafeStorageName == item.SafeStorageName)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.DisplayName, request.DisplayName), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPut("media/{id:int}/content"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Replace(int id, IFormFile file, CancellationToken ct)
    {
        var item = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return NotFound();
        if (!Guid.TryParseExact(item.SafeStorageName, "N", out _) || file.Length is <= 0 or > 5 * 1024 * 1024)
            return BadRequest(new { message = "Choose an image up to 5 MB. · 请选择不超过5 MB的图片。" });
        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var count = await input.ReadAtLeastAsync(header, 12, false, ct);
        var mime = ImageMime(header, count);
        if (mime is null) return BadRequest(new { message = "Choose a PNG, JPEG or WebP image." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({item.SafeStorageName}, 0))", ct);
        if (!await CanManage(item.SafeStorageName, ct)) return Forbid();
        Directory.CreateDirectory(StorageDirectory);
        var path = Path.Combine(StorageDirectory, item.SafeStorageName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var pending = Path.Combine(StorageDirectory, Guid.NewGuid().ToString("N"));
        var backup = pending + ".backup";
        try
        {
            await using (var output = System.IO.File.Create(pending))
            {
                await output.WriteAsync(header.AsMemory(0, count), ct);
                await input.CopyToAsync(output, ct);
            }
            System.IO.File.Copy(path, backup);
            await db.MediaAssets.Where(m => m.SafeStorageName == item.SafeStorageName).ExecuteUpdateAsync(set => set
                .SetProperty(m => m.MimeType, mime).SetProperty(m => m.OriginalFilename, Path.GetFileName(file.FileName)), ct);
            System.IO.File.Move(pending, path, true);
            await transaction.CommitAsync(ct);
        }
        catch { if (System.IO.File.Exists(backup)) System.IO.File.Move(backup, path, true); throw; }
        finally { System.IO.File.Delete(pending); System.IO.File.Delete(backup); }
        return NoContent();
    }
    private static string? ImageMime(byte[] header, int count) =>
        count >= 8 && header.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
        : count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255 ? "image/jpeg"
        : count >= 12 && System.Text.Encoding.ASCII.GetString(header,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header,8,4) == "WEBP" ? "image/webp" : null;
    [HttpGet("media")]
    public async Task<IActionResult> Library([FromQuery] int page = 1, [FromQuery] string? q = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var allowed = Enum.GetValues<EntryStatus>().Where(status => EntryPermissionRules.CanView(User.GetEditorRole(), status)).ToArray();
        var query = db.MediaAssets.AsNoTracking().Where(m => db.Entries.Any(e => e.Id == m.EntryId && allowed.Contains(e.Status)));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(m => (m.DisplayName ?? m.OriginalFilename).Contains(q.Trim()));
        var uniqueIds = query.GroupBy(m => m.SafeStorageName).Select(group => group.Min(m => m.Id));
        query = query.Where(m => uniqueIds.Contains(m.Id));
        var total = await query.CountAsync(ct);
        var assets = await query.OrderByDescending(m => m.UploadedAtUtc).ThenByDescending(m => m.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        var items = new List<object>();
        foreach (var asset in assets) items.Add(await Summary(asset, ct));
        return Ok(new { items, total, page, pageSize = 25 });
    }
    private async Task<Entry?> EntryFor(MediaAsset m, CancellationToken ct) => m.EntryId is int id ? await db.Entries.FindAsync([id], ct) : null;

    [HttpGet("entries/{entryId:int}/media")]
    public async Task<IActionResult> List(int entryId, CancellationToken ct)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), entry.Status)) return NotFound();
        var items = await db.MediaAssets.AsNoTracking().Where(m => m.EntryId == entryId).OrderByDescending(m => m.UploadedAtUtc).ToListAsync(ct);
        var results = new List<object>();
        foreach (var item in items) results.Add(await Summary(item, ct));
        return Ok(results);
    }
    private async Task<(string? Error, int? SegmentId)> ValidatePlacement(int entryId, string? placement, CancellationToken ct)
    {
        int? insertedIntoSegmentId = null;
        if (placement?.StartsWith("inline:", StringComparison.Ordinal) == true)
        {
            try
            {
                using var document = JsonDocument.Parse(placement[7..]);
                var root = document.RootElement;
                var kind = root.GetProperty("kind").GetString();
                var field = root.GetProperty("field").GetString();
                var nodeId = root.GetProperty("nodeId").GetInt32();
                var offset = root.GetProperty("offset").GetInt32();
                var before = root.GetProperty("before").GetString();
                var after = root.GetProperty("after").GetString();
                if (offset < 0 || before is null || after is null || before.Length > 32 || after.Length > 32
                    || !(kind == "sense" && field == "definition" || kind == "example" && field is "ka" or "zh"))
                    return ("Invalid image insertion position.", null);
                var expected = kind == "sense" ? SegmentType.Sense : SegmentType.Example;
                if (!await db.Segments.AnyAsync(s => s.Id == nodeId && s.EntryId == entryId && s.SegmentType == expected, ct))
                    return ("Save the target definition or example before inserting an image.", null);
                insertedIntoSegmentId = nodeId;
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return ("Invalid image insertion position.", null);
            }
        }
        return (null, insertedIntoSegmentId);
    }
    [HttpPost("entries/{entryId:int}/media"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int entryId, IFormFile file, [FromForm] string? placement, CancellationToken ct, [FromForm] string? displayName = null)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null) return NotFound();
        if (!EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        if (string.IsNullOrWhiteSpace(displayName)) return BadRequest(new { message = "შეიყვანეთ სურათის სახელი." });
        if (displayName.Length > 200) return BadRequest(new { message = "Image name must be at most 200 characters." });
        var normalizedName = displayName.Trim().ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var nameLock = "media-name:" + normalizedName;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({nameLock}, 0))", ct);
        if (await db.MediaAssets.AnyAsync(m => m.DisplayName != null && m.DisplayName.Trim().ToLower() == normalizedName, ct))
            return Conflict(new { message = "სახელი უკვე გამოყენებულია. აირჩიეთ სხვა სახელი ან გამოიყენეთ არსებული სურათი." });
        var position = await ValidatePlacement(entryId, placement, ct);
        if (position.Error is not null) return BadRequest(new { message = position.Error });
        var insertedIntoSegmentId = position.SegmentId;
        if (file.Length is <= 0 or > 5 * 1024 * 1024) return BadRequest(new { message = "Image must be at most 5 MB." });
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var count = await stream.ReadAtLeastAsync(header, 12, throwOnEndOfStream: false, cancellationToken: ct);
        var mime = count >= 8 && header.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
            : count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255 ? "image/jpeg"
            : count >= 12 && System.Text.Encoding.ASCII.GetString(header,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header,8,4) == "WEBP" ? "image/webp" : null;
        if (mime is null) return BadRequest(new { message = "Choose a PNG, JPEG or WebP image." });
        Directory.CreateDirectory(StorageDirectory);
        var key = Guid.NewGuid().ToString("N");
        var path = Path.Combine(StorageDirectory, key);
        try
        {
            await using (var output = System.IO.File.Create(path))
            {
                await output.WriteAsync(header.AsMemory(0,count), ct);
                await stream.CopyToAsync(output, ct);
            }
            var item = new MediaAsset { EntryId = entryId, OriginalFilename = Path.GetFileName(file.FileName), DisplayName = displayName,
                SafeStorageName = key, FileType = MediaFileType.Image, MimeType = mime, Placement = placement, InsertedIntoSegmentId = insertedIntoSegmentId,
                UploadedByEditorId = User.GetEditorId(), UploadedAtUtc = DateTime.UtcNow };
            db.MediaAssets.Add(item);
            await db.SaveChangesAsync(ct);
            item.StorageUrl = $"/media/{item.Id}/content";
            await db.SaveChangesAsync(ct);
            var result = await Summary(item, ct);
            await transaction.CommitAsync(ct);
            return Ok(result);
        }
        catch { System.IO.File.Delete(path); throw; }
    }
    public record ReuseMediaRequest(int MediaId, string? Placement);
    [HttpPost("entries/{entryId:int}/media/reuse")]
    public async Task<IActionResult> Reuse(int entryId, ReuseMediaRequest request, CancellationToken ct)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null) return NotFound();
        if (!EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        var source = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.MediaId, ct);
        if (source is null) return NotFound();
        var sourceEntry = await EntryFor(source, ct);
        if (sourceEntry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), sourceEntry.Status)) return NotFound();
        var position = await ValidatePlacement(entryId, request.Placement, ct);
        if (position.Error is not null) return BadRequest(new { message = position.Error });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({source.SafeStorageName}, 0))", ct);
        // A rename or replacement may have finished while reuse waited for the shared-file lock.
        source = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.MediaId, ct);
        if (source is null || !System.IO.File.Exists(Path.Combine(StorageDirectory, source.SafeStorageName))) return NotFound();
        var item = new MediaAsset { EntryId = entryId, InsertedIntoSegmentId = position.SegmentId,
            OriginalFilename = source.OriginalFilename, DisplayName = source.DisplayName, SafeStorageName = source.SafeStorageName,
            FileType = source.FileType, MimeType = source.MimeType, Placement = request.Placement,
            UploadedByEditorId = source.UploadedByEditorId, UploadedAtUtc = source.UploadedAtUtc };
        db.MediaAssets.Add(item);
        await db.SaveChangesAsync(ct);
        item.StorageUrl = $"/media/{item.Id}/content";
        await db.SaveChangesAsync(ct);
        var result = await Summary(item, ct);
        await transaction.CommitAsync(ct);
        return Ok(result);
    }

    [HttpGet("media/{id:int}/content")]
    public async Task<IActionResult> Content(int id, CancellationToken ct)
    {
        var item = await db.MediaAssets.FindAsync([id], ct);
        if (item is null) return NotFound();
        var entry = await EntryFor(item, ct);
        if (entry is null || !EntryPermissionRules.CanView(User.GetEditorRole(), entry.Status)) return NotFound();
        if (!Guid.TryParseExact(item.SafeStorageName, "N", out _)) return NotFound();
        var path = Path.Combine(StorageDirectory, item.SafeStorageName);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(path, item.MimeType);
    }
    [HttpDelete("media/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await db.MediaAssets.FindAsync([id], ct);
        if (item is null) return NotFound();
        var entry = await EntryFor(item, ct);
        if (entry is null || !EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({item.SafeStorageName}, 0))", ct);
        db.MediaAssets.Remove(item);
        await db.SaveChangesAsync(ct);
        if (!await db.MediaAssets.AnyAsync(m => m.SafeStorageName == item.SafeStorageName, ct) && Guid.TryParseExact(item.SafeStorageName, "N", out _)) System.IO.File.Delete(Path.Combine(StorageDirectory, item.SafeStorageName));
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
