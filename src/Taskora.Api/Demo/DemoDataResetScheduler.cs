using TodoApp.Application.Projects;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

namespace TodoApp.Api.Demo;

/// <summary>
/// Background hosted service that periodically restores the public demo
/// workspace (see <see cref="PublicDemoSeeder"/>, entirely separate from
/// DevelopmentDataSeeder's real/local data) to its pristine seeded state, so
/// visitors using the "View demo" role logins don't inherit clutter left
/// behind by earlier visitors. Disabled by default — must be explicitly
/// opted into via "PublicDemo:ResetScheduler:Enabled" since it destructively
/// deletes and re-seeds fixed demo content on a timer. Progress is published
/// to <see cref="DemoDataResetSchedulerStatus"/> for the Operations
/// dashboard/API to surface.
/// </summary>
public sealed class DemoDataResetScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    DemoDataResetSchedulerStatus status,
    ILogger<DemoDataResetScheduler> logger)
    : BackgroundService
{
    /// <summary>
    /// Entry point invoked by the host when the service starts. Reads
    /// configuration and records the initial status; if enabled, runs a reset
    /// on every tick of a <see cref="PeriodicTimer"/> until
    /// <paramref name="stoppingToken"/> is cancelled. Deliberately does not
    /// run immediately on startup — the demo workspace is already seeded
    /// synchronously earlier in Program.cs, so an immediate reset here would
    /// just be redundant extra work on every deploy.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = ReadBool(
            configuration["PublicDemo:ResetScheduler:Enabled"],
            false);
        var interval = TimeSpan.FromHours(Math.Max(
            1,
            ReadInt(
                configuration["PublicDemo:ResetScheduler:IntervalHours"],
                24)));

        if (!enabled)
        {
            status.Configure(false, interval, null);
            logger.LogInformation("Demo data reset scheduler is disabled.");
            return;
        }

        var nextRunAt = DateTimeOffset.UtcNow.Add(interval);
        status.Configure(true, interval, nextRunAt);
        logger.LogInformation(
            "Demo data reset scheduler enabled. Interval: {IntervalHours} hour(s).",
            interval.TotalHours);

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
        var startedAt = DateTimeOffset.UtcNow;
        status.MarkRunning(startedAt);
        logger.LogInformation("Demo data reset started at {StartedAt}.", startedAt);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider
                .GetRequiredService<TodoAppDbContext>();
            var deleteProjectHandler = scope.ServiceProvider
                .GetRequiredService<DeleteProjectHandler>();

            await PublicDemoSeeder.ResetContentAsync(
                context,
                deleteProjectHandler,
                cancellationToken);

            var completedAt = DateTimeOffset.UtcNow;
            var nextRunAt = completedAt.Add(interval);
            status.MarkSucceeded(completedAt, nextRunAt);
            logger.LogInformation(
                "Demo data reset completed. Next run: {NextRunAt}.",
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
                "Demo data reset failed. Next run: {NextRunAt}.",
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
