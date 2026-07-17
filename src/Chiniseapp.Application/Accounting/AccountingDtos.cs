namespace Chiniseapp.Application.Accounting;

/// <summary>One row of the 10.2 global accounting page.</summary>
public record EditorScoreSummary(
    int EditorId, string DisplayName, string Role,
    int MainScoreTotal, int AdditionalScoreTotal, int KaEditorScoreTotal);

/// <summary>10.2 entry status totals: count per status, plus the grand total.</summary>
public record EntryStatusTotals(IReadOnlyDictionary<string, int> CountsByStatus, int Total);

/// <summary>Counts by status (entry's *current* status), keyed for the personal accounting page.</summary>
public record ScoreCountsByStatus(
    IReadOnlyDictionary<string, int> Main,
    IReadOnlyDictionary<string, int> Additional,
    IReadOnlyDictionary<string, int> KaEditor);

public record EditorAccountingDetail(
    int EditorId, string DisplayName, string Email, string Role, DateTime CreatedAtUtc,
    ScoreCountsByStatus ScoresByStatus);
