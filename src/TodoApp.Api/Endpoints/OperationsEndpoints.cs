using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApp.Application.Abstractions;
using TodoApp.Api.Diagnostics;
using TodoApp.Api.Notifications;

namespace TodoApp.Api.Endpoints;

internal static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/operations")
            .WithTags("Operations")
            .RequireAuthorization();

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetOperationsSummary")
            .Produces<OperationsSummaryResponse>()
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetSummaryAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        HealthCheckService healthChecks,
        InMemoryLogStore logs,
        IWebHostEnvironment environment,
        DueDateReminderSchedulerStatus reminderStatus,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(
            currentUser.UserId,
            cancellationToken);
        if (account is null ||
            !IsSuperAdmin(account.User.Email, configuration))
        {
            return Results.Forbid();
        }

        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var checks = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new OperationHealthCheck(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description,
                entry.Value.Duration.TotalMilliseconds))
            .ToArray();
        var corsOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        if (corsOrigins.Length == 0 && environment.IsDevelopment())
        {
            corsOrigins = ["http://localhost:5173"];
        }
        var smtpEnabled = ReadBool(configuration["Email:Smtp:Enabled"]);
        var reminderSnapshot = reminderStatus.Snapshot;

        return Results.Ok(new OperationsSummaryResponse(
            true,
            DateTimeOffset.UtcNow,
            report.Status.ToString(),
            checks,
            new OperationsRuntime(
                environment.EnvironmentName,
                configuration["Database:Provider"] ?? "Sqlite",
                configuration["App:PublicBaseUrl"] ?? "http://localhost:5173",
                corsOrigins,
                smtpEnabled ? "SMTP" : "LogOnly",
                smtpEnabled,
                reminderSnapshot.Enabled,
                logs.RetentionDays,
                logs.MaxEntries),
            new ReminderSchedulerResponse(
                reminderSnapshot.Enabled,
                reminderSnapshot.Status,
                reminderSnapshot.IntervalMinutes,
                reminderSnapshot.LastRunStartedAt,
                reminderSnapshot.LastRunCompletedAt,
                reminderSnapshot.NextRunAt,
                reminderSnapshot.LastTaskReminderCount,
                reminderSnapshot.LastProjectReminderCount,
                reminderSnapshot.LastEmailCount,
                reminderSnapshot.LastError),
            logs.Recent(50)
                .Select(entry => new OperationLogRecord(
                    entry.Timestamp,
                    entry.Level,
                    entry.Category,
                    entry.Message,
                    entry.Exception,
                    entry.EventId))
                .ToArray()));
    }

    private static bool IsSuperAdmin(
        string email,
        IConfiguration configuration)
    {
        var emails = configuration
            .GetSection("Administration:SuperAdminEmails")
            .Get<string[]>() ?? [];
        var singleEmail = configuration["Administration:SuperAdminEmail"];
        if (!string.IsNullOrWhiteSpace(singleEmail))
        {
            emails = [.. emails, singleEmail];
        }

        return emails.Any(candidate =>
            email.Equals(
                candidate?.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReadBool(string? value) =>
        bool.TryParse(value, out var result) && result;
}

public sealed record OperationsSummaryResponse(
    bool IsSuperAdmin,
    DateTimeOffset GeneratedAt,
    string OverallHealth,
    IReadOnlyCollection<OperationHealthCheck> HealthChecks,
    OperationsRuntime Runtime,
    ReminderSchedulerResponse ReminderScheduler,
    IReadOnlyCollection<OperationLogRecord> RecentLogs);

public sealed record OperationHealthCheck(
    string Name,
    string Status,
    string? Description,
    double DurationMilliseconds);

public sealed record OperationsRuntime(
    string Environment,
    string DatabaseProvider,
    string PublicBaseUrl,
    IReadOnlyCollection<string> CorsAllowedOrigins,
    string EmailMode,
    bool SmtpEnabled,
    bool ReminderSchedulerEnabled,
    int LogRetentionDays,
    int LogMaxEntries);

public sealed record ReminderSchedulerResponse(
    bool Enabled,
    string Status,
    int IntervalMinutes,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    int LastTaskReminderCount,
    int LastProjectReminderCount,
    int LastEmailCount,
    string? LastError);

public sealed record OperationLogRecord(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? EventId);
