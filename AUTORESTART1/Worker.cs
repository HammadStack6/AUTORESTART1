using AUTORESTART1.Backup;
using AUTORESTART1.Scheduling;

public interface IBackupDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemBackupDelay(TimeProvider timeProvider) : IBackupDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);
}

public class BackupWorker : BackgroundService
{
    private readonly ILogger<BackupWorker> _logger;
    private readonly HourlyBackupSchedule _schedule;
    private readonly IBackupExecutor _backupExecutor;
    private readonly TimeProvider _timeProvider;
    private readonly IBackupDelay _delay;

    public BackupWorker(
        ILogger<BackupWorker> logger,
        HourlyBackupSchedule schedule,
        IBackupExecutor backupExecutor,
        TimeProvider timeProvider,
        IBackupDelay delay)
    {
        _logger = logger;
        _schedule = schedule;
        _backupExecutor = backupExecutor;
        _timeProvider = timeProvider;
        _delay = delay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup worker started.");
        DateTime minimumSearchTime = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = _timeProvider.GetLocalNow().DateTime;
            DateTime searchTime = now > minimumSearchTime ? now : minimumSearchTime;
            DateTime nextRun = _schedule.GetNextRun(searchTime);
            TimeSpan wait = nextRun - now;

            if (wait < TimeSpan.Zero)
                wait = TimeSpan.Zero;

            _logger.LogInformation(
                "Next backup is scheduled for {NextRun}; waiting {Wait}.",
                nextRun,
                wait);

            try
            {
                await _delay.DelayAsync(wait, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Starting scheduled backup for slot {ScheduledTime}.", nextRun);
                await _backupExecutor.ExecuteAsync(stoppingToken);
                _logger.LogInformation("Scheduled backup for {ScheduledTime} completed.", nextRun);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Scheduled backup for {ScheduledTime} failed.", nextRun);
            }

            minimumSearchTime = nextRun.AddTicks(1);
        }

        _logger.LogInformation("Backup worker stopped.");
    }
}
