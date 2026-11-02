using AUTORESTART1.Backup;

namespace AUTORESTART1.Tests.Backup;

public sealed class AtomicBackupFileCopierTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AUTORESTART1.Tests", Guid.NewGuid().ToString("N"));

    public AtomicBackupFileCopierTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task CopyAsync_replaces_an_existing_backup_only_after_copy_completes()
    {
        string source = Path.Combine(_directory, "source.mdb");
        string destination = Path.Combine(_directory, "att2026.mdb");
        await File.WriteAllTextAsync(source, "new database contents");
        await File.WriteAllTextAsync(destination, "old database contents");
        AtomicBackupFileCopier copier = new();

        await copier.CopyAsync(source, destination, CancellationToken.None);

        Assert.Equal("new database contents", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task CopyAsync_preserves_the_existing_backup_when_source_is_empty()
    {
        string source = Path.Combine(_directory, "empty.mdb");
        string destination = Path.Combine(_directory, "att2026.mdb");
        await File.WriteAllBytesAsync(source, []);
        await File.WriteAllTextAsync(destination, "old database contents");
        AtomicBackupFileCopier copier = new();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            copier.CopyAsync(source, destination, CancellationToken.None));

        Assert.Equal("old database contents", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
