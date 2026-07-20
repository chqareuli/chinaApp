using Chiniseapp.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("activity")]
[Authorize]
public class ActivityController(INotificationService notificationService) : ControllerBase
{
    [HttpGet("unread-counts")]
    public async Task<ActionResult<UnreadActivitySummary>> UnreadCounts(CancellationToken ct) =>
        Ok(await notificationService.GetUnreadActivityAsync(User.GetEditorId(), ct));
}
