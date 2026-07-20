namespace Chiniseapp.Application.PublicDictionary;

public interface IPublicDictionaryService
{
    Task<IReadOnlyList<PublicDictionarySearchItem>> SearchAsync(
        string query,
        PublicSearchMode mode,
        int limit,
        CancellationToken ct = default);

    Task<PublicDictionaryEntry?> GetEntryAsync(int id, CancellationToken ct = default);

    Task<PublicDictionaryStats> GetStatsAsync(CancellationToken ct = default);
}
