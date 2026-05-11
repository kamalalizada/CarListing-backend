namespace Entry.Services;

public static class ImageFileValidator
{
    private const int HeaderLength = 32;

    public static bool IsAllowedExtension(string extension)
    {
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> HasValidImageSignatureAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead)
            return false;

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        var header = new byte[HeaderLength];
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        if (stream.CanSeek)
            stream.Position = originalPosition;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => IsJpeg(header, bytesRead),
            ".png" => IsPng(header, bytesRead),
            ".webp" => IsWebp(header, bytesRead),
            ".avif" => IsAvif(header, bytesRead),
            _ => false
        };
    }

    private static bool IsJpeg(byte[] header, int bytesRead)
    {
        return bytesRead >= 3
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF;
    }

    private static bool IsPng(byte[] header, int bytesRead)
    {
        return bytesRead >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A;
    }

    private static bool IsWebp(byte[] header, int bytesRead)
    {
        return bytesRead >= 12
            && header[0] == 0x52
            && header[1] == 0x49
            && header[2] == 0x46
            && header[3] == 0x46
            && header[8] == 0x57
            && header[9] == 0x45
            && header[10] == 0x42
            && header[11] == 0x50;
    }

    private static bool IsAvif(byte[] header, int bytesRead)
    {
        if (bytesRead < 12)
            return false;

        var hasFtypBox = header[4] == 0x66
            && header[5] == 0x74
            && header[6] == 0x79
            && header[7] == 0x70;

        if (!hasFtypBox)
            return false;

        for (var i = 8; i <= bytesRead - 4; i++)
        {
            var isAvifBrand = header[i] == 0x61
                && header[i + 1] == 0x76
                && header[i + 2] == 0x69
                && header[i + 3] == 0x66;

            var isAvisBrand = header[i] == 0x61
                && header[i + 1] == 0x76
                && header[i + 2] == 0x69
                && header[i + 3] == 0x73;

            if (isAvifBrand || isAvisBrand)
                return true;
        }

        return false;
    }
}
