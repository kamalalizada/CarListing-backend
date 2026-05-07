namespace Entry.Services;

public interface IImageStorageService
{
    Task<ImageStorageResult> UploadAsync(
        int carId,
        Stream stream,
        long length,
        string fileExtension,
        string contentType,
        string? objectKey = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string? objectKey, CancellationToken cancellationToken = default);

    string? TryGetObjectKeyFromUrl(string imageUrl);
}
