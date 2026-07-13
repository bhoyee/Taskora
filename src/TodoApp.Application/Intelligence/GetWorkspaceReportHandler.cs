using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Intelligence;

public sealed record GetWorkspaceReportQuery(
    Guid WorkspaceId,
    DateOnly? From,
    DateOnly? To,
    Guid? ProjectId = null);

public sealed class GetWorkspaceReportHandler(
    IWorkspaceReportReadRepository reports)
{
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
