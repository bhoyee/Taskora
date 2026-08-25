using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Intelligence;

/// <summary>
/// Query for a workspace activity/report snapshot over an optional date
/// range, optionally narrowed to a single project.
/// </summary>
public sealed record GetWorkspaceReportQuery(
    Guid WorkspaceId,
    DateOnly? From,
    DateOnly? To,
    Guid? ProjectId = null);

/// <summary>Builds workspace reporting snapshots for a given date range.</summary>
public sealed class GetWorkspaceReportHandler(
    IWorkspaceReportReadRepository reports)
{
    /// <summary>
    /// Validates that a workspace identifier was supplied and, when both
    /// bounds are given, that the date range is not inverted (throws
    /// <see cref="ArgumentException"/> otherwise), then delegates to the read
    /// repository to build the workspace report snapshot.
    /// </summary>
    public Task<WorkspaceReportSnapshot> HandleAsync(
        GetWorkspaceReportQuery query,
        CancellationToken cancellationToken)
    {
        if (query.WorkspaceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Workspace identifier is required.",
                nameof(query));
        }

        if (query.From.HasValue &&
            query.To.HasValue &&
            query.From.Value > query.To.Value)
        {
            throw new ArgumentException(
                "Report start date cannot be after the end date.",
                nameof(query));
        }

        return reports.GetAsync(
            query.WorkspaceId,
            query.From,
            query.To,
            query.ProjectId,
            cancellationToken);
    }
}
