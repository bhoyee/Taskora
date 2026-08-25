namespace TodoApp.Application.Abstractions;

/// <summary>
/// Read-only repository for querying upcoming due dates and target dates that
/// require notifications to be sent out.
/// </summary>
public interface IDueDateNotificationReadRepository
{
    /// <summary>
    /// Retrieves the tasks whose due dates are approaching or have passed relative to <paramref name="today"/>
    /// and therefore require a due-date notification to be sent.
    /// </summary>
    /// <param name="today">The current business date to evaluate due dates against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tasks that qualify for a due-date notification.</returns>
    Task<IReadOnlyList<TaskDueNotification>> GetTaskDueNotificationsAsync(
        DateOnly today,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the projects whose target/delivery dates are approaching or have passed relative to
    /// <paramref name="today"/> and therefore require a target-date notification to be sent.
    /// </summary>
    /// <param name="today">The current business date to evaluate target dates against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The projects that qualify for a target-date notification.</returns>
    Task<IReadOnlyList<ProjectTargetNotification>> GetProjectTargetNotificationsAsync(
        DateOnly today,
        CancellationToken cancellationToken);
}

public sealed record TaskDueNotification(
    Guid TaskId,
    Guid ProjectId,
    string TaskTitle,
    DateOnly DueDate,
    int DaysUntilDue,
    IReadOnlyCollection<string> Recipients);

public sealed record ProjectTargetNotification(
    Guid ProjectId,
    string ProjectName,
    DateOnly TargetDate,
    int DaysUntilTarget,
    IReadOnlyCollection<string> Recipients);
