namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Exclusive editor role. Drives the status-transition table directly in C# —
/// deliberately not modeled as ASP.NET Identity's multi-role system.
/// </summary>
public enum EditorRole
{
    SuperAdmin,
    ChiefEditor,
    ZhEditor,
    KaEditor,
    AssistantEditor,
}
