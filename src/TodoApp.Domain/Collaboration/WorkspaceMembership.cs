namespace TodoApp.Domain.Collaboration;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership()
    {
    }

    internal WorkspaceMembership(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    internal void ChangeRole(WorkspaceRole role) => Role = role;
}
