using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TodoApp.Api.Notifications;

public sealed class DueDateReminderHealthCheck(
    DueDateReminderSchedulerStatus status)
    : IHealthCheck
{
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
