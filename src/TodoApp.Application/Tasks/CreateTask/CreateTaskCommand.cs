namespace TodoApp.Application.Tasks.CreateTask;

public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    DateOnly? DueDate = null,
    int? Effort = null);
