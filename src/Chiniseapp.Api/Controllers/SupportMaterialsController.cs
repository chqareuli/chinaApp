using Chiniseapp.Application.SupportMaterials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("entries/{entryId:int}/support-materials")]
[Authorize]
public class SupportMaterialsController(ISupportMaterialService supportMaterialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportMaterialSummary>>> Get(
        int entryId,
        CancellationToken ct) =>
        Ok(await supportMaterialService.GetForEntryAsync(entryId, ct));

    [HttpPost]
    public async Task<ActionResult<SupportMaterialSummary>> Add(
        int entryId,
        CreateSupportMaterialRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await supportMaterialService.AddAsync(
                entryId,
                request,
                User.GetEditorId(),
                ct);
            return CreatedAtAction(nameof(Get), new { entryId }, item);
        }
        catch (SupportMaterialValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
