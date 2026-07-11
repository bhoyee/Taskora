using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApp.Application.Abstractions;

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

        return Results.Ok(new OperationsSummaryResponse(
            true,
            DateTimeOffset.UtcNow,
            report.Status.ToString(),
            checks,
            new LoggingSummary(
                configuration["Logging:LogLevel:Default"] ?? "Information",
                configuration["Logging:LogLevel:Microsoft.AspNetCore"] ??
                    "Warning",
                "Application logs are emitted through ASP.NET Core ILogger providers. Use Azure App Service Log Stream or Application Insights in production.")));
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
}

public sealed record OperationsSummaryResponse(
    bool IsSuperAdmin,
    DateTimeOffset GeneratedAt,
    string OverallHealth,
    IReadOnlyCollection<OperationHealthCheck> HealthChecks,
    LoggingSummary Logging);

public sealed record OperationHealthCheck(
    string Name,
    string Status,
    string? Description,
    double DurationMilliseconds);

public sealed record LoggingSummary(
    string DefaultLevel,
    string AspNetCoreLevel,
    string Message);
