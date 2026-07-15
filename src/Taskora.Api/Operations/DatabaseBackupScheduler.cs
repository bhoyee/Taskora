namespace TodoApp.Api.Operations;

public sealed class DatabaseBackupScheduler(
    DatabaseBackupService backups,
    DatabaseBackupSchedulerStatus status,
    IConfiguration configuration,
    ILogger<DatabaseBackupScheduler> logger)
    : BackgroundService
{
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
            configuration["Operations:Backups:RunOnStartup"]);
        var nextRun = DateTimeOffset.UtcNow.Add(runOnStartup
            ? TimeSpan.Zero
            : interval);
        status.Scheduled(intervalHours, nextRun);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            var startedAt = DateTimeOffset.UtcNow;
            status.Started(startedAt);
            try
            {
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

    private static bool ReadBool(string? value) =>
        bool.TryParse(value, out var result) && result;

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : fallback;
}
