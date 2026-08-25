using TodoApp.Application.Abstractions;

namespace TodoApp.Api.Operations;

/// <summary>
/// Background service that periodically creates database backups via
/// <see cref="DatabaseBackupService"/> and publishes progress/results to
/// <see cref="DatabaseBackupSchedulerStatus"/> so the operations dashboard
/// can display scheduler health.
/// </summary>
public sealed class DatabaseBackupScheduler(
    DatabaseBackupService backups,
    DatabaseBackupSchedulerStatus status,
    IConfiguration configuration,
    IBusinessDateProvider dates,
    ILogger<DatabaseBackupScheduler> logger)
    : BackgroundService
{
    /// <summary>
    /// Runs for the lifetime of the host. If backups are disabled via
    /// configuration, marks the status as disabled and exits immediately.
    /// Otherwise loops forever: waits until the next scheduled run time,
    /// skips the run if a backup already exists for today (avoiding
    /// duplicates when the app restarts), creates a backup, and reschedules
    /// the next run based on the configured interval — recording success or
    /// failure on <see cref="DatabaseBackupSchedulerStatus"/> either way.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!backups.Enabled)
        {
            status.Disabled();
            logger.LogInformation("Database backup scheduler is disabled.");
            return;
        }

        var intervalHours = ReadPositiveInt(
            configuration["Operations:Backups:IntervalHours"],
            24);
        var interval = TimeSpan.FromHours(intervalHours);
        var runOnStartup = ReadBool(
            configuration["Operations:Backups:RunOnStartup"],
            true);
        var nextRun = DateTimeOffset.UtcNow.Add(runOnStartup
            ? TimeSpan.Zero
            : interval);
        status.Scheduled(intervalHours, nextRun);

        while (!stoppingToken.IsCancellationRequested)
        {
            // The hosted service sleeps until the next planned backup, then
            // records success/failure for the super-admin backup dashboard.
            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            var startedAt = DateTimeOffset.UtcNow;
            status.Started(startedAt);
            try
            {
                var backupDate = dates.Today;
                if (backups.HasBackupForDate(backupDate))
                {
                    nextRun = DateTimeOffset.UtcNow.Add(interval);
                    status.Scheduled(intervalHours, nextRun);
                    logger.LogInformation(
                        "Database backup skipped because a backup already exists for {BackupDate}. Next run: {NextRunAt}.",
                        backupDate,
                        nextRun);
                    continue;
                }

                var backup = await backups.CreateBackupAsync(stoppingToken);
                nextRun = DateTimeOffset.UtcNow.Add(interval);
                status.Completed(DateTimeOffset.UtcNow, nextRun, backup);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                nextRun = DateTimeOffset.UtcNow.Add(interval);
                logger.LogError(
                    exception,
                    "Automatic database backup failed.");
                status.Failed(DateTimeOffset.UtcNow, nextRun, exception);
            }
        }
    }

    // Parses a boolean configuration value, falling back when missing/invalid.
    private static bool ReadBool(string? value, bool fallback) =>
        bool.TryParse(value, out var result) ? result : fallback;

    // Parses a positive integer configuration value, falling back otherwise.
    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : fallback;
}
