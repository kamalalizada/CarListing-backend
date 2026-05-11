namespace Entry.Services;

public static class ImageFileValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".avif"
    };

    public static bool IsAllowedExtension(string extension)
    {
        return AllowedExtensions.Contains(NormalizeExtension(extension));
    }

    public static bool IsAllowedContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("image/avif", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasMatchingSignature(Stream stream, string extension)
    {
        if (!stream.CanSeek)
            return false;

        var normalizedExtension = NormalizeExtension(extension);
        Span<byte> header = stackalloc byte[16];

        var originalPosition = stream.Position;
        stream.Position = 0;
        var read = stream.Read(header);
        stream.Position = originalPosition;

        if (read < 12)
            return false;

        return normalizedExtension switch
        {
            ".jpg" or ".jpeg" => IsJpeg(header, read),
            ".png" => IsPng(header, read),
            ".webp" => IsWebp(header, read),
            ".avif" => IsAvif(header, read),
            _ => false
        };
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header, int read)
    {
        return read >= 3 &&
               header[0] == 0xFF &&
               header[1] == 0xD8 &&
               header[2] == 0xFF;
    }

    private static bool IsPng(ReadOnlySpan<byte> header, int read)
    {
        return read >= 8 &&
               header[0] == 0x89 &&
               header[1] == 0x50 &&
               header[2] == 0x4E &&
               header[3] == 0x47 &&
               header[4] == 0x0D &&
               header[5] == 0x0A &&
               header[6] == 0x1A &&
               header[7] == 0x0A;
    }

    private static bool IsWebp(ReadOnlySpan<byte> header, int read)
    {
        return read >= 12 &&
               header[0] == (byte)'R' &&
               header[1] == (byte)'I' &&
               header[2] == (byte)'F' &&
               header[3] == (byte)'F' &&
               header[8] == (byte)'W' &&
               header[9] == (byte)'E' &&
               header[10] == (byte)'B' &&
               header[11] == (byte)'P';
    }

    private static bool IsAvif(ReadOnlySpan<byte> header, int read)
    {
        if (read < 12)
            return false;

        var hasFtyp =
            header[4] == (byte)'f' &&
            header[5] == (byte)'t' &&
            header[6] == (byte)'y' &&
            header[7] == (byte)'p';

        if (!hasFtyp)
            return false;

        var isAvifBrand =
            header[8] == (byte)'a' &&
            header[9] == (byte)'v' &&
            header[10] == (byte)'i' &&
            (header[11] == (byte)'f' || header[11] == (byte)'s');

        return isAvifBrand;
    }
}
