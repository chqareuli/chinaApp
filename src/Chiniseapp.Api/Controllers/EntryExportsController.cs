using System.Text;
using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Api.Controllers;

[ApiController, Route("entries/export"), Authorize(Roles = nameof(EditorRole.SuperAdmin))]
public class EntryExportsController(ChiniseDbContext db, IEntryService entries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Export(string format, EntryStatus? status, int? editorId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (format is not ("json" or "xml" or "helper")) return BadRequest(new { message = "Unknown export format." });
        if (from > to || to == DateOnly.MaxValue) return BadRequest(new { message = "Check the date range." });
        if (status is EntryStatus.NewEntry or EntryStatus.Archived) return BadRequest(new { message = "This stage is not eligible for export." });
        var query = db.Entries.AsNoTracking().Where(e => e.Status != EntryStatus.NewEntry && e.Status != EntryStatus.Archived);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        if (editorId.HasValue) query = query.Where(e => e.LastEditorEditorId == editorId.Value);
        // Inclusive calendar dates in UTC, based on persisted last-modified time.
        if (from.HasValue) { var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); query = query.Where(e => e.UpdatedAtUtc >= start); }
        if (to.HasValue) { var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); query = query.Where(e => e.UpdatedAtUtc < end); }
        var ids = await query.OrderBy(e => e.Id).Select(e => e.Id).Take(5001).ToListAsync(ct);
        if (ids.Count > 5000) return BadRequest(new { message = "Select a narrower date or editor filter (maximum 5,000 entries per download)." });
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        if (format == "helper")
        {
            var helpers = await db.SupportMaterials.AsNoTracking().Where(h => ids.Contains(h.EntryId))
                .OrderBy(h => h.EntryId).ThenBy(h => h.Id).Select(h => new { h.Id, h.EntryId, h.Language, h.CommentText, h.CreatedAtUtc }).ToListAsync(ct);
            return File(JsonSerializer.SerializeToUtf8Bytes(helpers, json), "application/json", "chinese-ge-helper-materials.json");
        }
        var result = new List<EntryDetail>();
        foreach (var id in ids) { var entry = await entries.GetAsync(id, User.GetEditorRole(), ct); if (entry is not null) result.Add(entry); }
        return format == "xml"
            ? File(Encoding.UTF8.GetBytes(EntryExportSerializer.Xml(result)), "application/xml", "chinese-ge.xml")
            : File(JsonSerializer.SerializeToUtf8Bytes(result.Select(EntryExportSerializer.DictionaryContent), json), "application/json", "chinese-ge.json");
    }
}
