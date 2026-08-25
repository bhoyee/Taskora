namespace TodoApp.Domain.Tasks;

/// <summary>
/// Workflow states a <see cref="TaskItem"/> moves through from creation to completion.
/// </summary>
public enum TaskItemStatus
{
    /// <summary>Not yet scheduled for active work.</summary>
    Backlog = 0,

    /// <summary>Groomed and ready to be picked up.</summary>
    Ready = 1,

    /// <summary>Actively being worked on.</summary>
    InProgress = 2,

    /// <summary>Work has stalled on an external dependency or issue.</summary>
    Blocked = 3,

    /// <summary>Work is finished.</summary>
    Completed = 4
}
