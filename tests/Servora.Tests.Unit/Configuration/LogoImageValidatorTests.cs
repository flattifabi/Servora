using System.Text;
using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for logo type sniffing.</summary>
public sealed class LogoImageValidatorTests
{
    /// <summary>A minimal valid 1x1 PNG.</summary>
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public void DetectsRasterFormats()
    {
        Assert.Equal("image/png", LogoImageValidator.DetectContentType(Png));
        Assert.Equal("image/jpeg", LogoImageValidator.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]));
        Assert.Equal("image/gif", LogoImageValidator.DetectContentType("GIF89a\0\0\0\0\0\0"u8));
        Assert.Equal("image/webp", LogoImageValidator.DetectContentType("RIFF\0\0\0\0WEBPVP8 "u8));
    }

    [Fact]
    public void RejectsSvgHtmlAndTruncatedFiles()
    {
        Assert.Null(LogoImageValidator.DetectContentType(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")));
        Assert.Null(LogoImageValidator.DetectContentType(Encoding.UTF8.GetBytes("<html><body>hi</body></html>")));
        Assert.Null(LogoImageValidator.DetectContentType([0x89, 0x50]));
        Assert.Null(LogoImageValidator.DetectContentType([]));
    }
}
