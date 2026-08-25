using TodoApp.Application.Notifications;

namespace TodoApp.Api.Notifications;

/// <summary>
/// Background hosted service that periodically sends due-date reminder
/// notifications (tasks/projects) and personal-todo carry-over alerts.
/// Enabled/interval are read from configuration
/// ("Notifications:Scheduler:Enabled"/"IntervalMinutes"), and progress is
/// published to <see cref="DueDateReminderSchedulerStatus"/> for the
/// Operations dashboard/API to surface.
/// </summary>
public sealed class DueDateReminderScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    DueDateReminderSchedulerStatus status,
    ILogger<DueDateReminderScheduler> logger)
    : BackgroundService
{
    /// <summary>
    /// Entry point invoked by the host when the service starts. Reads
    /// configuration, records the initial status, runs one pass immediately,
    /// and then runs again on every tick of a <see cref="PeriodicTimer"/>
    /// until <paramref name="stoppingToken"/> is cancelled. If the scheduler
    /// is disabled via configuration, it records that and returns without
    /// running anything.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = ReadBool(
            configuration["Notifications:Scheduler:Enabled"],
            true);
        var interval = TimeSpan.FromMinutes(Math.Max(
            1,
            ReadInt(
                configuration["Notifications:Scheduler:IntervalMinutes"],
                1440)));

        if (!enabled)
        {
            status.Configure(false, interval, null);
            logger.LogInformation(
                "Automatic due-date reminder scheduler is disabled.");
            return;
        }

        var nextRunAt = DateTimeOffset.UtcNow.Add(interval);
        status.Configure(true, interval, nextRunAt);
        logger.LogInformation(
            "Automatic due-date reminder scheduler enabled. Interval: {IntervalMinutes} minute(s).",
            interval.TotalMinutes);

        await RunOnceAsync(interval, stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        // One scheduler pass covers both deadline reminders and My Day carryover
        // alerts, so Operations can show a single notification-run status.
        var startedAt = DateTimeOffset.UtcNow;
        status.MarkRunning(startedAt);
        logger.LogInformation(
            "Automatic due-date reminder run started at {StartedAt}.",
            startedAt);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dueDateHandler = scope.ServiceProvider
                .GetRequiredService<SendDueDateNotificationsHandler>();
            var carryOverHandler = scope.ServiceProvider
                .GetRequiredService<SendPersonalTodoCarryOverNotificationsHandler>();
            var dueDateResult = await dueDateHandler.HandleAsync(
                new SendDueDateNotificationsCommand(),
                cancellationToken);
            var carryOverResult = await carryOverHandler.HandleAsync(
                new SendPersonalTodoCarryOverNotificationsCommand(),
                cancellationToken);
            var completedAt = DateTimeOffset.UtcNow;
            var nextRunAt = completedAt.Add(interval);

            status.MarkSucceeded(
                completedAt,
                nextRunAt,
                dueDateResult.TaskReminderCount,
                dueDateResult.ProjectReminderCount,
                carryOverResult.TodoCarryOverCount,
                dueDateResult.EmailCount + carryOverResult.EmailCount);
            logger.LogInformation(
                "Automatic notification run completed. Task reminders: {TaskReminderCount}. Project reminders: {ProjectReminderCount}. Todo carryovers: {TodoCarryOverCount}. Emails: {EmailCount}. Next run: {NextRunAt}.",
                dueDateResult.TaskReminderCount,
                dueDateResult.ProjectReminderCount,
                carryOverResult.TodoCarryOverCount,
                dueDateResult.EmailCount + carryOverResult.EmailCount,
                nextRunAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.UtcNow;
            var nextRunAt = failedAt.Add(interval);
            status.MarkFailed(failedAt, nextRunAt, exception.Message);
            logger.LogError(
                exception,
                "Automatic due-date reminder run failed. Next run: {NextRunAt}.",
                nextRunAt);
        }
    }

    // Parses a config value as bool, falling back to defaultValue when
    // missing or unparseable.
    private static bool ReadBool(string? value, bool defaultValue) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    // Parses a config value as int, falling back to defaultValue when
    // missing or unparseable.
    private static int ReadInt(string? value, int defaultValue) =>
        int.TryParse(value, out var result) ? result : defaultValue;
}
