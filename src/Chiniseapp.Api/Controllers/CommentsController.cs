using Chiniseapp.Application.Comments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

public record UpdateCommentRequest(string CommentText);

/// <summary>Internal editorial notes tied to an entry — never public content.</summary>
[ApiController]
[Route("comments")]
[Authorize]
public class CommentsController(ICommentService commentService) : ControllerBase
{
    [HttpGet("/entries/{entryId:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentSummary>>> ForEntry(int entryId, CancellationToken ct) =>
        Ok(await commentService.GetForEntryAsync(entryId, ct));

    [HttpPost("/entries/{entryId:int}/comments")]
    public async Task<ActionResult<CommentSummary>> Add(int entryId, CreateCommentRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await commentService.AddAsync(entryId, request, User.GetEditorId(), ct));
        }
        catch (CommentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Only the comment's own author, or a Chief Editor/Super Admin, may edit it.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CommentSummary>> Update(int id, UpdateCommentRequest request, CancellationToken ct)
    {
        try
        {
            var comment = await commentService.UpdateTextAsync(id, request.CommentText, User.GetEditorId(), User.GetEditorRole(), ct);
            return comment is null ? NotFound() : Ok(comment);
        }
        catch (CommentAccessDeniedException)
        {
            return Problem(detail: "You can only edit your own comments.", statusCode: StatusCodes.Status403Forbidden);
        }
        catch (CommentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Only the comment's own author, or a Chief Editor/Super Admin, may archive it.</summary>
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        try
        {
            var archived = await commentService.ArchiveAsync(id, User.GetEditorId(), User.GetEditorRole(), ct);
            return archived ? NoContent() : NotFound();
        }
        catch (CommentAccessDeniedException)
        {
            return Problem(detail: "You can only archive your own comments.", statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
