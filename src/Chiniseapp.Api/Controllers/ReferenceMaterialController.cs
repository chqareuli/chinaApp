using Chiniseapp.Application.ReferenceMaterials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

/// <summary>
/// Auxiliary/legacy dictionary-prep material shown to the editor for reference only —
/// never part of the public entry content.
/// </summary>
[ApiController]
[Route("entries/{entryId:int}/reference-material")]
[Authorize]
public class ReferenceMaterialController(IReferenceMaterialService referenceMaterialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReferenceMaterialDetail>> Get(int entryId, CancellationToken ct)
    {
        var detail = await referenceMaterialService.GetForEntryAsync(entryId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPut]
    [Authorize(Roles = AuthorizationPolicies.ContentEditorRoles)]
    public async Task<ActionResult<ReferenceMaterialDetail>> Save(int entryId, SaveReferenceMaterialRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await referenceMaterialService.SaveAsync(entryId, request, ct));
        }
        catch (ReferenceMaterialValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
