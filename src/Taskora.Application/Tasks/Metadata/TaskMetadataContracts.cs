namespace TodoApp.Application.Tasks.Metadata;

/// <summary>DTO representing a project-scoped task category.</summary>
public sealed record ProjectCategoryDto(Guid Id, Guid ProjectId, string Name);

/// <summary>DTO representing a single note left on a task.</summary>
public sealed record TaskNoteDto(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Command to create a new category within a project.</summary>
public sealed record CreateCategoryCommand(Guid ProjectId, string Name);

/// <summary>Command to assign (or clear, when <c>CategoryId</c> is null) a task's category.</summary>
public sealed record UpdateTaskCategoryCommand(Guid TaskId, Guid? CategoryId);

/// <summary>Command to add a tag to a task.</summary>
public sealed record AddTaskTagCommand(Guid TaskId, string Name);

/// <summary>Command to remove a tag from a task.</summary>
public sealed record RemoveTaskTagCommand(Guid TaskId, string Name);

/// <summary>Command to add a note to a task, authored by the current user.</summary>
public sealed record AddTaskNoteCommand(Guid TaskId, string Body);
