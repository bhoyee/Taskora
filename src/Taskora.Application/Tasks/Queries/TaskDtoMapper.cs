using TodoApp.Domain.Tasks;
using TodoApp.Application.Tasks.Metadata;

namespace TodoApp.Application.Tasks.Queries;

internal static class TaskDtoMapper
{
    public static TaskListItemDto ToListItem(TaskItem task, DateOnly today) =>
        new(
            task.Id,
            task.ProjectId,
            task.CreatedByUserId,
            task.AssignedUserId,
            task.SprintId,
            task.CreatedAt,
            task.Title,
            task.CategoryId,
            task.Tags.Select(tag => tag.Name).ToArray(),
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
            task.CreatedByUserId,
            task.AssignedUserId,
            task.SprintId,
            task.CreatedAt,
            task.Title,
            task.CategoryId,
            task.Tags.Select(tag => tag.Name).ToArray(),
            task.Notes
                .OrderByDescending(note => note.CreatedAt)
                .Select(note => new TaskNoteDto(
                    note.Id,
                    note.TaskId,
                    note.AuthorId,
                    note.Body,
                    note.CreatedAt))
                .ToArray(),
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
