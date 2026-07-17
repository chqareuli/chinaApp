using System.Security.Claims;
using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Api;

public static class CurrentEditorExtensions
{
    public static int GetEditorId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static EditorRole GetEditorRole(this ClaimsPrincipal user) =>
        Enum.Parse<EditorRole>(user.FindFirstValue(ClaimTypes.Role)!);
}
