namespace Chiniseapp.Application.Vocabularies;

public interface IVocabularyService
{
    Task<IReadOnlyList<VocabularyItemDto>> GetActiveAsync(
        string? category = null,
        CancellationToken ct = default);
}
