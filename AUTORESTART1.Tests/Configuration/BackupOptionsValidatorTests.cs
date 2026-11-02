using AUTORESTART1.Configuration;

namespace AUTORESTART1.Tests.Configuration;

public sealed class BackupOptionsValidatorTests
{
    public static TheoryData<BackupOptions> InvalidOptions => new()
    {
        Valid() with { SourceFile = "" },
        Valid() with { BackupFolder = "" },
        Valid() with { BackupFileName = @"folder\att2026.mdb" },
        Valid() with { BackupFileName = "bad?.mdb" },
        Valid() with { StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(21, 0, 0) },
        Valid() with { IntervalMinutes = 0 },
        Valid() with { IntervalMinutes = int.MaxValue },
        Valid() with { CommandTimeoutSeconds = 0 },
        Valid() with { CommandTimeoutSeconds = int.MaxValue },
        Valid() with
        {
            SourceFile = @"C:\data\att2000.mdb",
            BackupFolder = @"C:\data",
            BackupFileName = "att2000.mdb"
        }
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_rejects_invalid_configuration(BackupOptions options)
    {
        BackupOptionsValidator validator = new();

        Assert.True(validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_accepts_valid_configuration()
    {
        BackupOptionsValidator validator = new();

        Assert.True(validator.Validate(null, Valid()).Succeeded);
    }

    private static BackupOptions Valid() => new()
    {
        SourceFile = @"C:\data\att2000.mdb",
        BackupFolder = @"D:\backup",
        BackupFileName = "att2026.mdb",
        StartTime = new TimeSpan(10, 0, 0),
        EndTime = new TimeSpan(21, 0, 0),
        IntervalMinutes = 60,
        CommandTimeoutSeconds = 120
    };
}
