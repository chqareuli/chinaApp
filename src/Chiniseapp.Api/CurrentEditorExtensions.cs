using System.Security.Claims;

namespace Chiniseapp.Api;

public static class CurrentEditorExtensions
{
    public static int GetEditorId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
