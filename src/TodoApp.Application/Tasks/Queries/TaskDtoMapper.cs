using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

internal static class TaskDtoMapper
{
    public static TaskListItemDto ToListItem(TaskItem task, DateOnly today) =>
        new(
            task.Id,
            task.ProjectId,
            task.AssignedUserId,
            task.Title,
            task.Status,
            task.IsBlocked,
            task.DueDate?.Value,
            task.HasPlanningFactors ? task.Priority.Value : null,
            task.HasPlanningFactors ? task.Priority.Band : null,
            ToExplanation(task),
            task.GetDeadlineHealth(today));

    public static TaskDetailsDto ToDetails(TaskItem task, DateOnly today) =>
        new(
            task.Id,
            task.ProjectId,
            task.AssignedUserId,
            task.Title,
            task.Status,
            task.IsBlocked,
            task.BlockedReason,
            task.DueDate?.Value,
            task.EffortEstimate?.Value,
            task.HasPlanningFactors ? task.Priority.Value : null,
            task.HasPlanningFactors ? task.Priority.Band : null,
            ToExplanation(task),
            task.GetDeadlineHealth(today),
            task.DependencyIds,
            task.CompletedAt);

    private static PriorityExplanationDto? ToExplanation(TaskItem task)
    {
        if (!task.HasPlanningFactors)
        {
            return null;
        }

        return new PriorityExplanationDto(
            task.Priority.Value,
            task.Priority.Band,
            task.PlanningFactors.Effort,
            task.Priority.BusinessValueContribution,
            task.Priority.UrgencyContribution,
            task.Priority.RiskReductionContribution);
    }
}
