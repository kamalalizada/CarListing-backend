using Entry.Services;

namespace CarListing.Tests;

public class ImageFileValidatorTests
{
    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".avif", "image/avif")]
    public void AllowedFormats_AreAccepted(string extension, string contentType)
    {
        Assert.True(ImageFileValidator.IsAllowedExtension(extension));
        Assert.True(ImageFileValidator.IsAllowedContentType(contentType));
    }

    [Fact]
    public void HasMatchingSignature_ReturnsTrue_ForPngHeader()
    {
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ];

        using var stream = new MemoryStream(bytes);

        Assert.True(ImageFileValidator.HasMatchingSignature(stream, ".png"));
    }

    [Fact]
    public void HasMatchingSignature_ReturnsFalse_WhenHeaderDoesNotMatchExtension()
    {
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ];

        using var stream = new MemoryStream(bytes);

        Assert.False(ImageFileValidator.HasMatchingSignature(stream, ".jpg"));
    }
}
