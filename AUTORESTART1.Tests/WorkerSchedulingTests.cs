using AUTORESTART1.Backup;
using AUTORESTART1.Configuration;
using AUTORESTART1.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AUTORESTART1.Tests;

public sealed class WorkerSchedulingTests
{
    [Fact]
    public async Task Worker_waits_for_the_next_aligned_slot_and_runs_one_backup()
    {
        BackupOptions options = new()
        {
            SourceFile = @"C:\data\att2000.mdb",
            BackupFolder = @"D:\backup",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(21, 0, 0),
            IntervalMinutes = 60
        };
        MutableTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 2, 10, 30, 0, TimeSpan.Zero));
        RecordingDelay delay = new(timeProvider);
        using CancellationTokenSource stopping = new();
        CancellingBackupExecutor executor = new(stopping);
        TestableBackupWorker worker = new(
            NullLogger<BackupWorker>.Instance,
            new HourlyBackupSchedule(options),
            executor,
            timeProvider,
            delay);

        await worker.RunAsync(stopping.Token);

        Assert.Equal([TimeSpan.FromMinutes(30)], delay.Delays);
        Assert.Equal(1, executor.ExecutionCount);
    }

    private sealed class TestableBackupWorker(
        ILogger<BackupWorker> logger,
        HourlyBackupSchedule schedule,
        IBackupExecutor executor,
        TimeProvider timeProvider,
        IBackupDelay delay)
        : BackupWorker(logger, schedule, executor, timeProvider, delay)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }

    private sealed class CancellingBackupExecutor(CancellationTokenSource stopping) : IBackupExecutor
    {
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            stopping.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDelay(MutableTimeProvider timeProvider) : IBackupDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            timeProvider.Advance(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialTime;

        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
