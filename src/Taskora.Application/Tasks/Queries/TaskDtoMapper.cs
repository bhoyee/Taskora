using TodoApp.Domain.Tasks;
using TodoApp.Application.Tasks.Metadata;

namespace TodoApp.Application.Tasks.Queries;

// Maps domain TaskItem entities to the read-side DTOs exposed by query handlers.
internal static class TaskDtoMapper
{
    // Maps a task to its list-view DTO, computing deadline health against the given date.
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

    // Maps a task to its full detail DTO, including notes (newest first) and dependencies.
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

    // Builds the priority breakdown DTO, or null if the task has no planning factors set.
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
