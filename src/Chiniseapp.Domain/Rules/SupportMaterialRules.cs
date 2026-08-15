namespace Chiniseapp.Domain.Rules;

public static class SupportMaterialRules
{
    public const int MaxCommentLength = 4000;

    private static readonly IReadOnlyDictionary<string, string> Languages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["English"] = "English",
            ["Chinese"] = "Chinese",
            ["Russian"] = "Russian",
            ["Other"] = "Other",
        };

    public static string? NormalizeLanguage(string? language)
    {
        var trimmed = language?.Trim();
        return trimmed is not null && Languages.TryGetValue(trimmed, out var canonical)
            ? canonical
            : null;
    }
}
