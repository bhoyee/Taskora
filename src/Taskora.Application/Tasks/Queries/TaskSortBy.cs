namespace TodoApp.Application.Tasks.Queries;

/// <summary>
/// Supported sort orders for task search/list results.
/// </summary>
public enum TaskSortBy
{
    /// <summary>Highest computed priority score first.</summary>
    PriorityDescending = 0,

    /// <summary>Earliest due date first (tasks without a due date sort last).</summary>
    DueDateAscending = 1,

    /// <summary>Alphabetical by title, A to Z.</summary>
    TitleAscending = 2,

    /// <summary>Most recently created task first.</summary>
    CreatedDescending = 3
}
