namespace TodoApp.Domain.Projects;

/// <summary>
/// Lifecycle states of a <see cref="Sprint"/>.
/// </summary>
public enum SprintStatus
{
    /// <summary>Sprint has been created but has not started yet.</summary>
    Planned = 0,

    /// <summary>Sprint is currently underway.</summary>
    Active = 1,

    /// <summary>Sprint has finished normally.</summary>
    Completed = 2,

    /// <summary>Sprint was terminated before completion.</summary>
    Cancelled = 3
}
