using Chiniseapp.Application.Accounting;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Accounting;

public class AccountingService(ChiniseDbContext db) : IAccountingService
{
    public async Task<IReadOnlyList<EditorScoreSummary>> GetGlobalSummaryAsync(CancellationToken ct = default)
    {
        var editors = await db.Editors.OrderBy(e => e.DisplayName).ToListAsync(ct);

        var counts = await db.ScoreEntries
            .GroupBy(s => new { s.EditorId, s.ScoreType })
            .Select(g => new { g.Key.EditorId, g.Key.ScoreType, Count = g.Count() })
            .ToListAsync(ct);

        int CountFor(int editorId, ScoreType type) =>
            counts.FirstOrDefault(c => c.EditorId == editorId && c.ScoreType == type)?.Count ?? 0;

        return editors
            .Select(e => new EditorScoreSummary(
                e.Id, e.DisplayName, e.Role.ToString(),
                CountFor(e.Id, ScoreType.Main),
                CountFor(e.Id, ScoreType.Additional),
                CountFor(e.Id, ScoreType.KaEditor)))
            .ToList();
    }

    public async Task<EntryStatusTotals> GetEntryStatusTotalsAsync(CancellationToken ct = default)
    {
        var counts = await db.Entries
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Every status appears in the response even with a zero count, so the
        // accounting page doesn't need to special-case "never happened yet".
        var byStatus = Enum.GetValues<EntryStatus>().ToDictionary(
            s => s.ToString(),
            s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);

        return new EntryStatusTotals(byStatus, byStatus.Values.Sum());
    }

    public async Task<EditorAccountingDetail?> GetEditorDetailAsync(int editorId, CancellationToken ct = default)
    {
        var editor = await db.Editors.FindAsync([editorId], ct);
        if (editor is null)
        {
            return null;
        }

        var scored = await (
            from s in db.ScoreEntries
            join e in db.Entries on s.EntryId equals e.Id
            where s.EditorId == editorId
            select new { s.ScoreType, e.Status }).ToListAsync(ct);

        Dictionary<string, int> CountsFor(ScoreType type) => scored
            .Where(x => x.ScoreType == type)
            .GroupBy(x => x.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var scoresByStatus = new ScoreCountsByStatus(
            CountsFor(ScoreType.Main),
            CountsFor(ScoreType.Additional),
            CountsFor(ScoreType.KaEditor));

        return new EditorAccountingDetail(
            editor.Id, editor.DisplayName, editor.Email, editor.Role.ToString(), editor.CreatedAtUtc, scoresByStatus);
    }
}
