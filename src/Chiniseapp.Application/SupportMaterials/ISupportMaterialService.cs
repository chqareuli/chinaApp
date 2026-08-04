using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Application.SupportMaterials;

public interface ISupportMaterialService
{
    Task<IReadOnlyList<SupportMaterialSummary>> GetForEntryAsync(
        int entryId,
        int requestingEditorId,
        EditorRole requestingRole,
        CancellationToken ct = default);

    Task<SupportMaterialSummary> AddAsync(
        int entryId,
        CreateSupportMaterialRequest request,
        int editorId,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int entryId,
        int materialId,
        int requestingEditorId,
        EditorRole requestingRole,
        CancellationToken ct = default);
}
