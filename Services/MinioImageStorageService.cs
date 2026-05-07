using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Entry.Services;

public sealed class MinioImageStorageService : IImageStorageService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;

    public MinioImageStorageService(IOptions<MinioOptions> options)
    {
        _options = options.Value;
        ValidateOptions(_options);

        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSsl)
            .Build();
    }

    public async Task<ImageStorageResult> UploadAsync(
        int carId,
        Stream stream,
        long length,
        string fileExtension,
        string contentType,
        string? objectKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        var extension = fileExtension.StartsWith('.') ? fileExtension : $".{fileExtension}";
        objectKey ??= $"cars/{carId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var putArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(putArgs, cancellationToken);

        return new ImageStorageResult(BuildPublicUrl(objectKey), objectKey);
    }

    public async Task DeleteAsync(string? objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return;

        await EnsureBucketAsync(cancellationToken);

        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectKey);

        await _client.RemoveObjectAsync(removeArgs, cancellationToken);
    }

    public string? TryGetObjectKeyFromUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var publicBaseUrl = _options.PublicBaseUrl.TrimEnd('/');
        if (!imageUrl.StartsWith(publicBaseUrl, StringComparison.OrdinalIgnoreCase))
            return null;

        return imageUrl[(publicBaseUrl.Length + 1)..];
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var existsArgs = new BucketExistsArgs().WithBucket(_options.BucketName);
        var exists = await _client.BucketExistsAsync(existsArgs, cancellationToken);
        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
            await _client.MakeBucketAsync(makeArgs, cancellationToken);
        }

        var policy = $$"""
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Effect": "Allow",
              "Principal": { "AWS": ["*"] },
              "Action": ["s3:GetObject"],
              "Resource": ["arn:aws:s3:::{{_options.BucketName}}/*"]
            }
          ]
        }
        """;

        var policyArgs = new SetPolicyArgs()
            .WithBucket(_options.BucketName)
            .WithPolicy(policy);

        await _client.SetPolicyAsync(policyArgs, cancellationToken);
    }

    private string BuildPublicUrl(string objectKey)
    {
        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{objectKey}";
    }

    private static void ValidateOptions(MinioOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new InvalidOperationException("Minio:Endpoint config boş ola bilməz.");
        if (string.IsNullOrWhiteSpace(options.AccessKey))
            throw new InvalidOperationException("Minio:AccessKey config boş ola bilməz.");
        if (IsPlaceholderSecret(options.AccessKey))
            throw new InvalidOperationException("Minio:AccessKey placeholder ola bilməz. Real dəyəri Minio__AccessKey environment variable və ya user-secrets ilə ver.");
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            throw new InvalidOperationException("Minio:SecretKey config boş ola bilməz.");
        if (IsPlaceholderSecret(options.SecretKey))
            throw new InvalidOperationException("Minio:SecretKey placeholder ola bilməz. Real dəyəri Minio__SecretKey environment variable və ya user-secrets ilə ver.");
        if (string.IsNullOrWhiteSpace(options.BucketName))
            throw new InvalidOperationException("Minio:BucketName config boş ola bilməz.");
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            throw new InvalidOperationException("Minio:PublicBaseUrl config boş ola bilməz.");
    }

    private static bool IsPlaceholderSecret(string value)
    {
        return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
    }
}
