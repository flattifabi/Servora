namespace Servora.Application.Configuration;

/// <summary>
/// Detects the image type of an uploaded logo from its magic bytes. Only raster formats are accepted (PNG, JPEG, GIF,
/// WebP); SVG is rejected because it can carry scripts. The declared content type and file name are never trusted.
/// </summary>
public static class LogoImageValidator
{
    /// <summary>The number of leading bytes needed for detection.</summary>
    public const int HeaderLength = 12;

    /// <summary>Detects the content type of an image.</summary>
    /// <param name="header">At least the first <see cref="HeaderLength"/> bytes of the file.</param>
    /// <returns>The content type, or <see langword="null"/> when the format is not an accepted image.</returns>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 6 && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
