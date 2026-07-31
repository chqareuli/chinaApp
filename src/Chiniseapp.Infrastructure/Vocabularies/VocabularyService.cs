using Chiniseapp.Application.Vocabularies;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chiniseapp.Infrastructure.Vocabularies;

public class VocabularyService(ChiniseDbContext db) : IVocabularyService
{
    public async Task<IReadOnlyList<VocabularyItemDto>> GetActiveAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        VocabularyCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<VocabularyCategory>(category, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return [];
            }

            parsedCategory = parsed;
        }

        var query = db.ControlledVocabularies
            .AsNoTracking()
            .Where(item => item.IsActive);

        if (parsedCategory.HasValue)
        {
            query = query.Where(item => item.Category == parsedCategory.Value);
        }

        return await query
            .OrderBy(item => item.Category)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => new VocabularyItemDto(
                item.Id,
                item.Category.ToString(),
                item.Code,
                item.DisplayZh,
                item.SortOrder))
            .ToListAsync(ct);
    }
}
