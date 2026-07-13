namespace TodoApp.Application.Abstractions;

public interface IDueDateNotificationReadRepository
{
    Task<IReadOnlyList<TaskDueNotification>> GetTaskDueNotificationsAsync(
        DateOnly today,
        CancellationToken cancellationToken);

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
