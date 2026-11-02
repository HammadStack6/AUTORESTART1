using AUTORESTART1.Configuration;

namespace AUTORESTART1.Scheduling;

public sealed class HourlyBackupSchedule
{
    private readonly BackupOptions _options;

    public HourlyBackupSchedule(BackupOptions options)
    {
        _options = options;
    }

    public DateTime GetNextRun(DateTime now)
    {
        DateTime start = now.Date.Add(_options.StartTime);
        DateTime end = now.Date.Add(_options.EndTime);

        if (now <= start)
            return start;

        if (now > end)
            return now.Date.AddDays(1).Add(_options.StartTime);

        long intervalTicks = TimeSpan.FromMinutes(_options.IntervalMinutes).Ticks;
        long elapsedTicks = (now - start).Ticks;
        long intervalsElapsed = (elapsedTicks + intervalTicks - 1) / intervalTicks;
        DateTime candidate = start.AddTicks(intervalsElapsed * intervalTicks);

        return candidate <= end
            ? candidate
            : now.Date.AddDays(1).Add(_options.StartTime);
    }
}
