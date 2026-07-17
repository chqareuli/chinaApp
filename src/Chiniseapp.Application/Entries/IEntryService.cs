using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Application.Entries;

public interface IEntryService
{
    /// <summary>Minimum viable create: headword only, per spec 9 (title is the only required field).</summary>
    Task<EntryDetail> CreateAsync(CreateEntryRequest request, int currentEditorId, CancellationToken ct = default);

    Task<EntryDetail?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Throws <see cref="EntryConcurrencyException"/> if RowVersion is stale.</summary>
    Task<EntryDetail> SaveContentAsync(int id, SaveEntryContentRequest request, int currentEditorId, CancellationToken ct = default);

    /// <summary>5.1 Main editor page list: all entries except new_entry, status priority then last-modified desc.</summary>
    Task<PagedResult<EntrySummary>> GetMainListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>5.2 Search dropdown: starts-with title match, includes new_entry, title-length-first sort.</summary>
    Task<IReadOnlyList<EntrySummary>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="EntryStatusTransitionForbiddenException"/> if the current editor's role
    /// isn't allowed to make this transition (see Domain.Rules.StatusTransitionRules).
    /// </summary>
    Task<EntryDetail> ChangeStatusAsync(int id, EntryStatus targetStatus, int currentEditorId, CancellationToken ct = default);
}
