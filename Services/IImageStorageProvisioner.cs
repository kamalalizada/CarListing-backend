namespace Entry.Services;

public interface IImageStorageProvisioner
{
    Task ProvisionAsync(CancellationToken cancellationToken = default);
}
