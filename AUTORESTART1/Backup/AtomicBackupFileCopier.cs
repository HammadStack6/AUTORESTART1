namespace AUTORESTART1.Backup;

public sealed class AtomicBackupFileCopier : IBackupFileCopier
{
    public async Task CopyAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        FileInfo sourceInfo = new(sourceFile);
        if (!sourceInfo.Exists)
            throw new FileNotFoundException("The snapshot database file was not found.", sourceFile);

        if (sourceInfo.Length == 0)
            throw new InvalidDataException("The snapshot database file is empty.");

        string? destinationDirectory = Path.GetDirectoryName(destinationFile);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new InvalidOperationException("The backup destination directory is invalid.");

        Directory.CreateDirectory(destinationDirectory);
        string temporaryFile = $"{destinationFile}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (FileStream source = new(
                sourceFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 1024 * 1024,
                useAsync: true))
            await using (FileStream destination = new(
                temporaryFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            FileInfo temporaryInfo = new(temporaryFile);
            if (temporaryInfo.Length == 0 || temporaryInfo.Length != sourceInfo.Length)
                throw new InvalidDataException("The temporary backup file is incomplete.");

            if (File.Exists(destinationFile))
                File.Replace(temporaryFile, destinationFile, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(temporaryFile, destinationFile);

            File.SetLastWriteTime(destinationFile, DateTime.Now);
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
        }
    }
}
