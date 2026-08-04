namespace Chiniseapp.Application.SupportMaterials;

public interface ISupportMaterialService
{
    Task<IReadOnlyList<SupportMaterialSummary>> GetForEntryAsync(
        int entryId,
        CancellationToken ct = default);

    Task<SupportMaterialSummary> AddAsync(
        int entryId,
        CreateSupportMaterialRequest request,
        int editorId,
        CancellationToken ct = default);
}
