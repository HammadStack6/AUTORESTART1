namespace AUTORESTART1.Backup;

public sealed record ShadowCopy(string Id, string DeviceObject);

public interface IShadowCopyService
{
    Task<ShadowCopy> CreateAsync(string sourceFile, CancellationToken cancellationToken);
    Task DeleteAsync(ShadowCopy shadowCopy, CancellationToken cancellationToken);
}
