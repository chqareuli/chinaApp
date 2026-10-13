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
        m.UploadedByEditorId, UploadedByDisplayName = await db.Editors.Where(e => e.Id == m.UploadedByEditorId).Select(e => e.DisplayName).FirstOrDefaultAsync(ct), m.UploadedAtUtc
    };
    [HttpGet("media")]
    public async Task<IActionResult> Library([FromQuery] int page = 1, [FromQuery] string? q = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var allowed = Enum.GetValues<EntryStatus>().Where(status => EntryPermissionRules.CanView(User.GetEditorRole(), status)).ToArray();
        var query = db.MediaAssets.AsNoTracking().Where(m => db.Entries.Any(e => e.Id == m.EntryId && allowed.Contains(e.Status)));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(m => (m.DisplayName ?? m.OriginalFilename).Contains(q.Trim()));
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
    [HttpPost("entries/{entryId:int}/media"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int entryId, IFormFile file, [FromForm] string? placement, CancellationToken ct, [FromForm] string? displayName = null)
    {
        var entry = await db.Entries.FindAsync([entryId], ct);
        if (entry is null) return NotFound();
        if (!EntryPermissionRules.CanEditContent(User.GetEditorRole(), entry.Status)) return Forbid();
        displayName = displayName?.Trim();
        if (displayName?.Length > 200) return BadRequest(new { message = "Image name must be at most 200 characters." });
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
                    return BadRequest(new { message = "Invalid image insertion position." });
                var expected = kind == "sense" ? SegmentType.Sense : SegmentType.Example;
                if (!await db.Segments.AnyAsync(s => s.Id == nodeId && s.EntryId == entryId && s.SegmentType == expected, ct))
                    return BadRequest(new { message = "Save the target definition or example before inserting an image." });
                insertedIntoSegmentId = nodeId;
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return BadRequest(new { message = "Invalid image insertion position." });
            }
        }
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
            var item = new MediaAsset { EntryId = entryId, OriginalFilename = Path.GetFileName(file.FileName), DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(file.FileName) : displayName,
                SafeStorageName = key, FileType = MediaFileType.Image, MimeType = mime, Placement = placement, InsertedIntoSegmentId = insertedIntoSegmentId,
                UploadedByEditorId = User.GetEditorId(), UploadedAtUtc = DateTime.UtcNow };
            db.MediaAssets.Add(item);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            item.StorageUrl = $"/media/{item.Id}/content";
            await db.SaveChangesAsync(ct);
            var result = await Summary(item, ct);
            await transaction.CommitAsync(ct);
            return Ok(result);
        }
        catch { System.IO.File.Delete(path); throw; }
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
        db.MediaAssets.Remove(item);
        await db.SaveChangesAsync(ct);
        if (Guid.TryParseExact(item.SafeStorageName, "N", out _)) System.IO.File.Delete(Path.Combine(StorageDirectory, item.SafeStorageName));
        return NoContent();
    }
}
