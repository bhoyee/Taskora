using TodoApp.Domain.Tasks;

namespace TodoApp.Application.Tasks.Queries;

public sealed record TaskSearchResult(
    IReadOnlyList<TaskItem> Items,
    int TotalCount);
