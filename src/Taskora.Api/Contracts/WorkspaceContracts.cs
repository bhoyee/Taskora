using TodoApp.Domain.Collaboration;

namespace TodoApp.Api.Contracts;

/// <summary>Request body for inviting/adding a member to a workspace by email with a given role.</summary>
public sealed record AddWorkspaceMemberRequest(
    string Email,
    WorkspaceRole Role);

/// <summary>Request body for changing an existing workspace member's role.</summary>
public sealed record ChangeWorkspaceRoleRequest(WorkspaceRole Role);
