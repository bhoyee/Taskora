namespace TodoApp.Domain.Collaboration;

/// <summary>
/// Links a user to a workspace with a specific <see cref="WorkspaceRole"/>. Created
/// and mutated only through the owning <see cref="Workspace"/> aggregate.
/// </summary>
public sealed class WorkspaceMembership
{
    // Reserved for ORM materialization; domain code must use the parameterized constructor.
    private WorkspaceMembership()
    {
    }

    /// <summary>
    /// Creates a membership. Internal because memberships are only ever created by the
    /// <see cref="Workspace"/> aggregate root, which owns the collection.
    /// </summary>
    internal WorkspaceMembership(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    // Updates the member's role; restricted to the owning Workspace aggregate.
    internal void ChangeRole(WorkspaceRole role) => Role = role;
}
