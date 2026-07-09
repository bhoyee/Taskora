using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Collaboration;

public sealed record GetMyWorkspacesQuery;

public sealed record CreateWorkspaceCommand(string Name);

public sealed record GetWorkspaceMembersQuery(Guid WorkspaceId);

public sealed record AddWorkspaceMemberCommand(
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role);

public sealed record ChangeWorkspaceRoleCommand(
    Guid WorkspaceId,
    Guid UserId,
    WorkspaceRole Role);

public sealed record RemoveWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid UserId);

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    WorkspaceRole Role);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string DisplayName,
    string Email,
    WorkspaceRole Role);
