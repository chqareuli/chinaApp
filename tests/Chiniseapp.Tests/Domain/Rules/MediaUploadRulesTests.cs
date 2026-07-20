using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class MediaUploadRulesTests
{
    [Theory]
    [InlineData("image/jpeg", "photo.jpg")]
    [InlineData("image/png", "图片.png")]
    [InlineData("image/webp", "სურათი.webp")]
    [InlineData("image/gif", "animation.gif")]
    public void Supported_image_metadata_is_accepted(string mimeType, string filename)
    {
        Assert.Empty(MediaUploadRules.ValidateMetadata(filename, mimeType, 1024));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    [InlineData("text/html")]
    public void Unsupported_types_are_rejected(string mimeType)
    {
        Assert.NotEmpty(MediaUploadRules.ValidateMetadata("file.bin", mimeType, 1024));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MediaUploadRules.MaxFileSizeBytes + 1)]
    public void Invalid_sizes_are_rejected(long length)
    {
        Assert.NotEmpty(MediaUploadRules.ValidateMetadata("photo.jpg", "image/jpeg", length));
    }

    [Fact]
    public void Valid_png_signature_is_accepted()
    {
        byte[] bytes = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

        Assert.True(MediaUploadRules.HasValidSignature("image/png", bytes));
    }

    [Fact]
    public void Mismatched_signature_is_rejected()
    {
        Assert.False(MediaUploadRules.HasValidSignature("image/png", "<script>"u8));
    }
}
