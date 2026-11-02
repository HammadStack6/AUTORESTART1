namespace AUTORESTART1.Backup;

public interface IBackupFileCopier
{
    Task CopyAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken);
}
