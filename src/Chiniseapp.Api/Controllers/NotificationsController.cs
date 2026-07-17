using Chiniseapp.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationSummary>>> List(
        [FromQuery] bool unreadOnly = false, CancellationToken ct = default) =>
        Ok(await notificationService.GetForEditorAsync(User.GetEditorId(), unreadOnly, ct));

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct) =>
        await notificationService.MarkReadAsync(id, User.GetEditorId(), ct) ? NoContent() : NotFound();
}
