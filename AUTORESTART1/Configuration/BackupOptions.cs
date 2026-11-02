namespace AUTORESTART1.Configuration;

public sealed record BackupOptions
{
    public const string SectionName = "BackupSettings";

    public string SourceFile { get; init; } = string.Empty;
    public string BackupFolder { get; init; } = string.Empty;
    public string BackupFileName { get; init; } = "att2026.mdb";
    public TimeSpan StartTime { get; init; } = new(10, 0, 0);
    public TimeSpan EndTime { get; init; } = new(21, 0, 0);
    public int IntervalMinutes { get; init; } = 60;
    public int CommandTimeoutSeconds { get; init; } = 120;
}
