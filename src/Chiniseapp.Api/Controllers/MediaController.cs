using Chiniseapp.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("media")]
[Authorize]
public class MediaController(IMediaService mediaService) : ControllerBase
{
    [HttpGet("files/{safeStorageName}")]
    public async Task<IActionResult> Content(string safeStorageName, CancellationToken ct)
    {
        var content = await mediaService.GetContentAsync(safeStorageName, ct);
        return content is null
            ? NotFound()
            : File(content.Content, content.MimeType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            return await mediaService.DeleteAsync(id, User.GetEditorId(), ct) ? NoContent() : NotFound();
        }
        catch (MediaAccessDeniedException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (MediaValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
