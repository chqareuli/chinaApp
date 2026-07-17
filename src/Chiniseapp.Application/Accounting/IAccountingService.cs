namespace Chiniseapp.Application.Accounting;

public interface IAccountingService
{
    /// <summary>10.2 global accounting page: every editor, roles, score totals.</summary>
    Task<IReadOnlyList<EditorScoreSummary>> GetGlobalSummaryAsync(CancellationToken ct = default);

    /// <summary>10.2 entry status totals: count of entries per status, plus the overall total.</summary>
    Task<EntryStatusTotals> GetEntryStatusTotalsAsync(CancellationToken ct = default);

    /// <summary>10.2 personal accounting page. Null if the editor doesn't exist.</summary>
    Task<EditorAccountingDetail?> GetEditorDetailAsync(int editorId, CancellationToken ct = default);
}
