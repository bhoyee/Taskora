using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TodoApp.Api.Notifications;

/// <summary>
/// ASP.NET Core health check that reports the status of the background due-date
/// reminder scheduler, surfacing it as Degraded when disabled or when the last
/// run failed, and Healthy otherwise.
/// </summary>
public sealed class DueDateReminderHealthCheck(
    DueDateReminderSchedulerStatus status)
    : IHealthCheck
{
    /// <summary>
    /// Evaluates the current scheduler status snapshot and returns the
    /// corresponding health result (Degraded if the scheduler is disabled or its
    /// last run failed, Healthy otherwise).
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = status.Snapshot;
        if (!snapshot.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Automatic due-date reminder scheduler is disabled."));
        }

        if (snapshot.Status == "Failed")
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                snapshot.LastError ?? "Last automatic reminder run failed."));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Automatic scheduler {snapshot.Status.ToLowerInvariant()}. Next run: {snapshot.NextRunAt?.ToString("O") ?? "pending"}."));
    }
}
