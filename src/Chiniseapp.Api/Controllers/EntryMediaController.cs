using Chiniseapp.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("entries/{entryId:int}/media")]
[Authorize]
public class EntryMediaController(IMediaService mediaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MediaAssetItem>>> List(int entryId, CancellationToken ct)
    {
        try
        {
            return Ok(await mediaService.ListForEntryAsync(entryId, ct));
        }
        catch (MediaValidationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MediaRequestLimits.MaxRequestSizeBytes)]
    public async Task<ActionResult<MediaAssetItem>> Upload(
        int entryId,
        [FromForm] IFormFile file,
        [FromForm] string? placement,
        CancellationToken ct)
    {
        try
        {
            await using var content = file.OpenReadStream();
            var item = await mediaService.UploadAsync(
                entryId,
                new UploadEntryMediaCommand(content, file.FileName, file.ContentType, file.Length, placement),
                User.GetEditorId(),
                ct);
            return Created(item.StorageUrl, item);
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

internal static class MediaRequestLimits
{
    public const long MaxRequestSizeBytes = Chiniseapp.Domain.Rules.MediaUploadRules.MaxFileSizeBytes + 64 * 1024;
}
