using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

internal static class TaskDtoMapper
{
    public static TaskListItemDto ToListItem(TaskItem task) =>
        new(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Status,
            task.IsBlocked,
            task.DueDate?.Value,
            task.HasPlanningFactors ? task.Priority.Value : null);

    public static TaskDetailsDto ToDetails(TaskItem task) =>
        new(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Status,
            task.IsBlocked,
            task.BlockedReason,
            task.DueDate?.Value,
            task.EffortEstimate?.Value,
            task.HasPlanningFactors ? task.Priority.Value : null,
            task.DependencyIds,
            task.CompletedAt);
}
