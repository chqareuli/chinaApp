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
        Ok(await supportMaterialService.GetForEntryAsync(
            entryId,
            User.GetEditorId(),
            User.GetEditorRole(),
            ct));

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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int entryId, int id, CancellationToken ct)
    {
        try
        {
            var deleted = await supportMaterialService.DeleteAsync(
                entryId,
                id,
                User.GetEditorId(),
                User.GetEditorRole(),
                ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (SupportMaterialAccessDeniedException)
        {
            return Problem(
                detail: "You can only delete your own support materials.",
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
