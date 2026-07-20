namespace Chiniseapp.Domain.Rules;

public static class MediaUploadRules
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

    public static IReadOnlyList<string> ValidateMetadata(string? filename, string? mimeType, long length)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(filename))
        {
            errors.Add("A filename is required.");
        }

        if (length <= 0 || length > MaxFileSizeBytes)
        {
            errors.Add("The image must be between 1 byte and 10 MB.");
        }

        if (string.IsNullOrWhiteSpace(mimeType) || !AllowedExtensions.ContainsKey(mimeType))
        {
            errors.Add("Only JPEG, PNG, WebP, and GIF images are allowed.");
        }

        return errors;
    }

    public static bool HasValidSignature(string mimeType, ReadOnlySpan<byte> bytes) =>
        mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/gif" => bytes.Length >= 6 &&
                (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)),
            "image/webp" => bytes.Length >= 12 &&
                bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };

    public static string ExtensionFor(string mimeType) => AllowedExtensions[mimeType];
}
