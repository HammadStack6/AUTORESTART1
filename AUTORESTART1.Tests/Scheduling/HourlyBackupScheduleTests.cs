using AUTORESTART1.Configuration;
using AUTORESTART1.Scheduling;

namespace AUTORESTART1.Tests.Scheduling;

public sealed class HourlyBackupScheduleTests
{
    private static readonly BackupOptions Options = new()
    {
        SourceFile = @"C:\data\att2000.mdb",
        BackupFolder = @"D:\backup",
        StartTime = new TimeSpan(10, 0, 0),
        EndTime = new TimeSpan(21, 0, 0),
        IntervalMinutes = 60
    };

    private readonly HourlyBackupSchedule _schedule = new(Options);

    [Theory]
    [InlineData(2026, 11, 2, 9, 30, 2026, 11, 2, 10, 0)]
    [InlineData(2026, 11, 2, 10, 35, 2026, 11, 2, 11, 0)]
    [InlineData(2026, 11, 2, 10, 0, 2026, 11, 2, 10, 0)]
    [InlineData(2026, 11, 2, 21, 0, 2026, 11, 2, 21, 0)]
    [InlineData(2026, 11, 2, 21, 1, 2026, 11, 3, 10, 0)]
    [InlineData(2026, 11, 2, 13, 15, 2026, 11, 2, 14, 0)]
    public void GetNextRun_returns_the_next_aligned_slot(
        int year, int month, int day, int hour, int minute,
        int expectedYear, int expectedMonth, int expectedDay, int expectedHour, int expectedMinute)
    {
        DateTime now = new(year, month, day, hour, minute, 0, DateTimeKind.Local);
        DateTime expected = new(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute, 0, DateTimeKind.Local);

        Assert.Equal(expected, _schedule.GetNextRun(now));
    }

    [Fact]
    public void GetNextRun_does_not_schedule_past_end_when_interval_does_not_divide_window()
    {
        BackupOptions options = new()
        {
            SourceFile = @"C:\data\att2000.mdb",
            BackupFolder = @"D:\backup",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IntervalMinutes = 40
        };
        HourlyBackupSchedule schedule = new(options);

        DateTime result = schedule.GetNextRun(new DateTime(2026, 11, 2, 10, 50, 0, DateTimeKind.Local));

        Assert.Equal(new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Local), result);
    }
}
