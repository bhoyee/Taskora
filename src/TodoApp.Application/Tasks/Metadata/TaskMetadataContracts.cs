namespace TodoApp.Application.Tasks.Metadata;

public sealed record ProjectCategoryDto(Guid Id, Guid ProjectId, string Name);

public sealed record TaskNoteDto(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record CreateCategoryCommand(Guid ProjectId, string Name);

public sealed record UpdateTaskCategoryCommand(Guid TaskId, Guid? CategoryId);

public sealed record AddTaskTagCommand(Guid TaskId, string Name);

public sealed record RemoveTaskTagCommand(Guid TaskId, string Name);

public sealed record AddTaskNoteCommand(Guid TaskId, string Body);
