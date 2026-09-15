using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Application.Entries;

public record EntryListFilter(
    int Page = 1, int PageSize = 25, string? Query = null, EntryStatus? Status = null,
    int? EditorId = null, int? AuthorId = null, bool ExcludeDrafts = false, string Sort = "UpdatedDesc",
    int? WorkEditorId = null, string? ScoreKind = null);
