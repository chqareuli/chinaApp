using Chiniseapp.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

/// <summary>Plain internal messaging between editors (§7: "like Facebook"), independent of any entry.</summary>
[ApiController]
[Route("messages")]
[Authorize]
public class MessagesController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DirectMessageSummary>>> Inbox(CancellationToken ct) =>
        Ok(await notificationService.GetInboxAsync(User.GetEditorId(), ct));

    [HttpPost]
    public async Task<ActionResult<DirectMessageSummary>> Send(SendMessageRequest request, CancellationToken ct)
    {
        try
        {
            var message = await notificationService.SendMessageAsync(User.GetEditorId(), request, ct);
            return Ok(message);
        }
        catch (NotificationValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct) =>
        await notificationService.MarkMessageReadAsync(id, User.GetEditorId(), ct) ? NoContent() : NotFound();
}
