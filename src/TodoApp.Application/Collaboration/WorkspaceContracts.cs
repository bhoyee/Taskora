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

public sealed record InviteWorkspaceMemberCommand(
    Guid WorkspaceId,
    string FullName,
    string Email,
    WorkspaceRole Role);

public sealed record GetWorkspaceInvitationsQuery(Guid WorkspaceId);

public sealed record GetWorkspaceInvitationByTokenQuery(string Token);

public sealed record AcceptWorkspaceInvitationCommand(
    string Token,
    string? DisplayName,
    string? Password);

public sealed record DeclineWorkspaceInvitationCommand(string Token);

public sealed record CancelWorkspaceInvitationCommand(
    Guid WorkspaceId,
    Guid InvitationId);

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    WorkspaceRole Role);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string DisplayName,
    string Email,
    WorkspaceRole Role);

public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string FullName,
    string Email,
    WorkspaceRole Role,
    WorkspaceInvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? InviteLink);
