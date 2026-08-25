using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApp.Application.Abstractions;
using TodoApp.Api.Authorization;
using TodoApp.Api.Diagnostics;
using TodoApp.Api.Notifications;
using TodoApp.Api.Operations;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers the operations diagnostics and database backup routes under
/// "/api/v1/operations". Every route requires an authenticated caller, and
/// the handlers additionally gate access to super-admins (as determined by
/// <see cref="SuperAdminAuthorization.IsSuperAdminAsync"/>, which checks the
/// caller's account email against the configured super-admin email list).
/// </summary>
internal static class OperationsEndpoints
{
    /// <summary>
    /// Maps the operations endpoint group. All routes require authentication
    /// via <c>RequireAuthorization()</c>; the individual handlers perform the
    /// stricter super-admin check before returning any data.
    /// </summary>
    public static IEndpointRouteBuilder MapOperationsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/operations")
            .WithTags("Operations")
            .RequireAuthorization();

        // GET /api/v1/operations/summary
        // Super-admin only. Returns a snapshot of app health, runtime
        // configuration, scheduler status, and recent logs
        // (200 OperationsSummaryResponse, 403 for non super-admins).
        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetOperationsSummary")
            .Produces<OperationsSummaryResponse>()
            .Produces(StatusCodes.Status403Forbidden);
        // GET /api/v1/operations/backups
        // Super-admin only. Lists the database backup files on disk
        // (200 collection of DatabaseBackupFile, 403 for non super-admins).
        group.MapGet("/backups", ListBackupsAsync)
            .WithName("ListDatabaseBackups")
            .Produces<IReadOnlyCollection<DatabaseBackupFile>>()
            .Produces(StatusCodes.Status403Forbidden);
        // POST /api/v1/operations/backups
        // Super-admin only. Triggers an on-demand database backup
        // (200 DatabaseBackupFile describing the new backup, 403 for non
        // super-admins).
        group.MapPost("/backups", CreateBackupAsync)
            .WithName("CreateDatabaseBackup")
            .Produces<DatabaseBackupFile>()
            .Produces(StatusCodes.Status403Forbidden);
        // GET /api/v1/operations/backups/{fileName}
        // Super-admin only. Streams a previously created backup file
        // (200 file download, 404 if the file does not exist, 403 for non
        // super-admins).
        group.MapGet("/backups/{fileName}", DownloadBackupAsync)
            .WithName("DownloadDatabaseBackup")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    // Handler for GET /api/v1/operations/summary. Gated to super-admins;
    // assembles health check results, runtime/config info, both background
    // scheduler snapshots, and the most recent in-memory log entries into a
    // single response for the operations dashboard.
    private static async Task<IResult> GetSummaryAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        HealthCheckService healthChecks,
        InMemoryLogStore logs,
        IWebHostEnvironment environment,
        DueDateReminderSchedulerStatus reminderStatus,
        DatabaseBackupSchedulerStatus backupStatus,
        IBusinessDateProvider dates,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser, accounts, configuration, cancellationToken))
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
        var backupSnapshot = backupStatus.Snapshot;

        return Results.Ok(new OperationsSummaryResponse(
            true,
            DateTimeOffset.UtcNow,
            report.Status.ToString(),
            checks,
            new OperationsRuntime(
                environment.EnvironmentName,
                configuration["Database:Provider"] ?? "Sqlite",
                configuration["App:PublicBaseUrl"] ?? "http://localhost:5173",
                dates.TimeZoneId,
                corsOrigins,
                smtpEnabled ? "SMTP" : "LogOnly",
                smtpEnabled,
                reminderSnapshot.Enabled,
                logs.RetentionDays,
                logs.MaxEntries,
                ReadBool(configuration["Operations:Logs:FileEnabled"], true),
                configuration["Operations:Logs:Directory"] ?? "App_Data/logs"),
            new ReminderSchedulerResponse(
                reminderSnapshot.Enabled,
                reminderSnapshot.Status,
                reminderSnapshot.IntervalMinutes,
                reminderSnapshot.LastRunStartedAt,
                reminderSnapshot.LastRunCompletedAt,
                reminderSnapshot.NextRunAt,
                reminderSnapshot.LastTaskReminderCount,
                reminderSnapshot.LastProjectReminderCount,
                reminderSnapshot.LastTodoCarryOverCount,
                reminderSnapshot.LastEmailCount,
                reminderSnapshot.LastError),
            new DatabaseBackupSchedulerResponse(
                backupSnapshot.Enabled,
                backupSnapshot.Status,
                backupSnapshot.IntervalHours,
                backupSnapshot.LastRunStartedAt,
                backupSnapshot.LastRunCompletedAt,
                backupSnapshot.NextRunAt,
                backupSnapshot.LastBackupFileName,
                backupSnapshot.LastBackupSizeBytes,
                backupSnapshot.LastError),
            logs.Recent(50)
                .Select(entry => new OperationLogRecord(
                    entry.Timestamp,
                    entry.Level,
                    entry.Category,
                    entry.Message,
                    entry.Exception,
                    entry.EventId,
                    entry.CorrelationId))
                .ToArray()));
    }

    // Handler for GET /api/v1/operations/backups. Gated to super-admins;
    // delegates to DatabaseBackupService to enumerate backup files on disk.
    private static async Task<IResult> ListBackupsAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        DatabaseBackupService backups,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser,
                accounts,
                configuration,
                cancellationToken))
        {
            return Results.Forbid();
        }

        return Results.Ok(await backups.ListBackupsAsync(cancellationToken));
    }

    // Handler for POST /api/v1/operations/backups. Gated to super-admins;
    // triggers an immediate on-demand backup via DatabaseBackupService.
    private static async Task<IResult> CreateBackupAsync(
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        DatabaseBackupService backups,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser,
                accounts,
                configuration,
                cancellationToken))
        {
            return Results.Forbid();
        }

        return Results.Ok(await backups.CreateBackupAsync(cancellationToken));
    }

    // Handler for GET /api/v1/operations/backups/{fileName}. Gated to
    // super-admins; streams the requested backup file back as a download, or
    // 404 if no such file exists in the configured backup directory.
    private static async Task<IResult> DownloadBackupAsync(
        string fileName,
        ICurrentUser currentUser,
        IAccountRepository accounts,
        IConfiguration configuration,
        DatabaseBackupService backups,
        CancellationToken cancellationToken)
    {
        if (!await SuperAdminAuthorization.IsSuperAdminAsync(
                currentUser,
                accounts,
                configuration,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var file = backups.GetBackupFile(fileName);
        return file is null
            ? Results.NotFound()
            : Results.File(
                file.FullName,
                "application/json",
                file.Name,
                enableRangeProcessing: true);
    }

    // Parses a boolean configuration value, falling back when missing/invalid.
    private static bool ReadBool(string? value, bool defaultValue = false) =>
        bool.TryParse(value, out var result) ? result : defaultValue;
}

/// <summary>Full operations dashboard payload returned to super-admins.</summary>
public sealed record OperationsSummaryResponse(
    bool IsSuperAdmin,
    DateTimeOffset GeneratedAt,
    string OverallHealth,
    IReadOnlyCollection<OperationHealthCheck> HealthChecks,
    OperationsRuntime Runtime,
    ReminderSchedulerResponse ReminderScheduler,
    DatabaseBackupSchedulerResponse DatabaseBackups,
    IReadOnlyCollection<OperationLogRecord> RecentLogs);

/// <summary>Result of a single ASP.NET Core health check entry.</summary>
public sealed record OperationHealthCheck(
    string Name,
    string Status,
    string? Description,
    double DurationMilliseconds);

/// <summary>Snapshot of the running app's environment and configuration.</summary>
public sealed record OperationsRuntime(
    string Environment,
    string DatabaseProvider,
    string PublicBaseUrl,
    string TimeZoneId,
    IReadOnlyCollection<string> CorsAllowedOrigins,
    string EmailMode,
    bool SmtpEnabled,
    bool ReminderSchedulerEnabled,
    int LogRetentionDays,
    int LogMaxEntries,
    bool LogFileEnabled,
    string LogDirectory);

/// <summary>Status snapshot of the due-date reminder background scheduler.</summary>
public sealed record ReminderSchedulerResponse(
    bool Enabled,
    string Status,
    int IntervalMinutes,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    int LastTaskReminderCount,
    int LastProjectReminderCount,
    int LastTodoCarryOverCount,
    int LastEmailCount,
    string? LastError);

/// <summary>Status snapshot of the automatic database backup scheduler.</summary>
public sealed record DatabaseBackupSchedulerResponse(
    bool Enabled,
    string Status,
    int IntervalHours,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    string? LastBackupFileName,
    long LastBackupSizeBytes,
    string? LastError);

/// <summary>A single recent log entry surfaced on the operations dashboard.</summary>
public sealed record OperationLogRecord(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? EventId,
    string? CorrelationId);
