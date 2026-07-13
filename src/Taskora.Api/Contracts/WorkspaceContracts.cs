using TodoApp.Domain.Collaboration;

namespace TodoApp.Api.Contracts;

public sealed record AddWorkspaceMemberRequest(
    string Email,
    WorkspaceRole Role);

public sealed record ChangeWorkspaceRoleRequest(WorkspaceRole Role);
