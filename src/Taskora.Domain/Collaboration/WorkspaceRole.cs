namespace TodoApp.Domain.Collaboration;

/// <summary>
/// Permission level a user holds within a workspace, in increasing order of authority.
/// </summary>
public enum WorkspaceRole
{
    /// <summary>Standard participant with baseline access to workspace content.</summary>
    Member = 0,

    /// <summary>Elevated role able to manage workspace content and membership.</summary>
    Manager = 1,

    /// <summary>Highest authority; typically the workspace creator with full control.</summary>
    Owner = 2
}
