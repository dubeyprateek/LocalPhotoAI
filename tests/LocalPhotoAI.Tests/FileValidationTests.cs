using LocalPhotoAI.Shared.Security;

namespace LocalPhotoAI.Tests;

public class FileValidationTests
{
    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("photo.png", true)]
    [InlineData("photo.gif", true)]
    [InlineData("photo.bmp", true)]
    [InlineData("photo.webp", true)]
    [InlineData("photo.heic", true)]
    [InlineData("photo.heif", true)]
    [InlineData("photo.tiff", true)]
    [InlineData("photo.tif", true)]
    [InlineData("photo.JPG", true)]
    [InlineData("photo.Png", true)]
    public void IsAllowedExtension_AllowedExtensions_ReturnsTrue(string fileName, bool expected)
    {
        Assert.Equal(expected, FileValidation.IsAllowedExtension(fileName));
    }

    [Theory]
    [InlineData("script.exe")]
    [InlineData("malware.bat")]
    [InlineData("hack.ps1")]
    [InlineData("payload.dll")]
    [InlineData("document.pdf")]
    [InlineData("archive.zip")]
    [InlineData("page.html")]
    [InlineData("code.cs")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsAllowedExtension_DisallowedExtensions_ReturnsFalse(string fileName)
    {
        Assert.False(FileValidation.IsAllowedExtension(fileName));
    }

    [Theory]
    [InlineData("photo.jpg", "photo.jpg")]
    [InlineData("my photo.png", "my photo.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Windows\\System32\\evil.jpg", "evil.jpg")]
    [InlineData("/tmp/hack.png", "hack.png")]
    [InlineData("..\\..\\secret.jpg", "secret.jpg")]
    public void SanitizeFileName_RemovesPathComponents(string input, string expected)
    {
        var result = FileValidation.SanitizeFileName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "unnamed")]
    [InlineData("   ", "unnamed")]
    [InlineData(null, "unnamed")]
    public void SanitizeFileName_EmptyOrNull_ReturnsUnnamed(string? input, string expected)
    {
        var result = FileValidation.SanitizeFileName(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFileName_InvalidCharsReplaced()
    {
        // Characters like < > : " | ? * are invalid on Windows
        var result = FileValidation.SanitizeFileName("photo<test>.jpg");
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.EndsWith(".jpg", result);
    }

    [Fact]
    public void GetAllowedExtensions_ReturnsNonEmptySet()
    {
        var extensions = FileValidation.GetAllowedExtensions();
        Assert.NotEmpty(extensions);
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".png", extensions);
    }
}
