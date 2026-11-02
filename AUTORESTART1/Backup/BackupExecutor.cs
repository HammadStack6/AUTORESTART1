using AUTORESTART1.Configuration;

namespace AUTORESTART1.Backup;

public interface IBackupExecutor
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

public sealed class BackupExecutor : IBackupExecutor
{
    private readonly BackupOptions _options;
    private readonly IShadowCopyService _shadowCopyService;
    private readonly IBackupFileCopier _fileCopier;
    private readonly ILogger<BackupExecutor> _logger;

    public BackupExecutor(
        BackupOptions options,
        IShadowCopyService shadowCopyService,
        IBackupFileCopier fileCopier,
        ILogger<BackupExecutor> logger)
    {
        _options = options;
        _shadowCopyService = shadowCopyService;
        _fileCopier = fileCopier;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        ShadowCopy? shadowCopy = null;
        Exception? operationException = null;

        try
        {
            shadowCopy = await _shadowCopyService.CreateAsync(_options.SourceFile, cancellationToken);

            string sourceRoot = Path.GetPathRoot(_options.SourceFile)
                ?? throw new InvalidOperationException("The source database path has no drive root.");
            string relativeSource = Path.GetRelativePath(sourceRoot, _options.SourceFile);
            string snapshotSource = Path.Combine(shadowCopy.DeviceObject + "\\", relativeSource);
            string destination = Path.Combine(_options.BackupFolder, _options.BackupFileName);

            await _fileCopier.CopyAsync(snapshotSource, destination, cancellationToken);
            _logger.LogInformation("Backup successfully replaced {DestinationFile}.", destination);
        }
        catch (Exception exception)
        {
            operationException = exception;
            throw;
        }
        finally
        {
            if (shadowCopy is not null)
            {
                try
                {
                    await _shadowCopyService.DeleteAsync(shadowCopy, CancellationToken.None);
                }
                catch (Exception cleanupException) when (operationException is not null)
                {
                    _logger.LogError(
                        cleanupException,
                        "Failed to delete shadow copy {ShadowId} after backup failure.",
                        shadowCopy.Id);
                }
            }
        }
    }
}
