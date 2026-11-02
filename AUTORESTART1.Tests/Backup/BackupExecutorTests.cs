using AUTORESTART1.Backup;
using AUTORESTART1.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AUTORESTART1.Tests.Backup;

public sealed class BackupExecutorTests
{
    private static readonly BackupOptions Options = new()
    {
        SourceFile = @"C:\data\att2000.mdb",
        BackupFolder = @"D:\backup",
        BackupFileName = "att2026.mdb"
    };

    [Fact]
    public async Task ExecuteAsync_copies_from_the_snapshot_and_deletes_that_snapshot()
    {
        RecordingShadowService shadows = new();
        RecordingCopier copier = new();
        BackupExecutor executor = CreateExecutor(shadows, copier);

        await executor.ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy42\data\att2000.mdb",
            copier.SourceFile);
        Assert.Equal(@"D:\backup\att2026.mdb", copier.DestinationFile);
        Assert.Same(shadows.Created, shadows.Deleted);
    }

    [Fact]
    public async Task ExecuteAsync_deletes_its_snapshot_when_copy_fails()
    {
        RecordingShadowService shadows = new();
        RecordingCopier copier = new() { Failure = new IOException("copy failed") };
        BackupExecutor executor = CreateExecutor(shadows, copier);

        await Assert.ThrowsAsync<IOException>(() => executor.ExecuteAsync(CancellationToken.None));

        Assert.Same(shadows.Created, shadows.Deleted);
    }

    private static BackupExecutor CreateExecutor(IShadowCopyService shadows, IBackupFileCopier copier) => new(
        Options,
        shadows,
        copier,
        NullLogger<BackupExecutor>.Instance);

    private sealed class RecordingShadowService : IShadowCopyService
    {
        public ShadowCopy Created { get; } = new("{id}", @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy42");
        public ShadowCopy? Deleted { get; private set; }

        public Task<ShadowCopy> CreateAsync(string sourceFile, CancellationToken cancellationToken) =>
            Task.FromResult(Created);

        public Task DeleteAsync(ShadowCopy shadowCopy, CancellationToken cancellationToken)
        {
            Deleted = shadowCopy;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCopier : IBackupFileCopier
    {
        public string? SourceFile { get; private set; }
        public string? DestinationFile { get; private set; }
        public Exception? Failure { get; init; }

        public Task CopyAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
        {
            SourceFile = sourceFile;
            DestinationFile = destinationFile;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
