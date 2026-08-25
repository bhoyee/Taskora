using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Collaboration;

/// <summary>Query to list all workspaces the current user belongs to.</summary>
public sealed record GetMyWorkspacesQuery;

/// <summary>Command to create a new workspace owned by the current user.</summary>
public sealed record CreateWorkspaceCommand(string Name);

/// <summary>Command to rename a workspace, optionally bypassing membership/ownership checks for administrative use.</summary>
public sealed record UpdateWorkspaceCommand(
    Guid WorkspaceId,
    string Name,
    bool HasAdministrativeBypass = false);

/// <summary>Command to delete a workspace, optionally bypassing membership/ownership checks for administrative use.</summary>
public sealed record DeleteWorkspaceCommand(
    Guid WorkspaceId,
    bool HasAdministrativeBypass = false);

/// <summary>Command to suspend a workspace, with an optional reason.</summary>
public sealed record SuspendWorkspaceCommand(
    Guid WorkspaceId,
    string? Reason);

/// <summary>Command to lift a suspension on a workspace.</summary>
public sealed record ReactivateWorkspaceCommand(Guid WorkspaceId);

/// <summary>Query to list a workspace's members, optionally bypassing membership checks for administrative use.</summary>
public sealed record GetWorkspaceMembersQuery(
    Guid WorkspaceId,
    bool HasAdministrativeBypass = false);

/// <summary>Command to add an existing user directly to a workspace by email.</summary>
public sealed record AddWorkspaceMemberCommand(
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role);

/// <summary>Command to change a workspace member's role.</summary>
public sealed record ChangeWorkspaceRoleCommand(
    Guid WorkspaceId,
    Guid UserId,
    WorkspaceRole Role);

/// <summary>Command to remove a member from a workspace.</summary>
public sealed record RemoveWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid UserId);

/// <summary>Command to invite a (possibly new) user to join a workspace by email.</summary>
public sealed record InviteWorkspaceMemberCommand(
    Guid WorkspaceId,
    string FullName,
    string Email,
    WorkspaceRole Role);

/// <summary>Query to list all invitations issued for a workspace.</summary>
public sealed record GetWorkspaceInvitationsQuery(Guid WorkspaceId);

/// <summary>Query to look up a pending workspace invitation by its token.</summary>
public sealed record GetWorkspaceInvitationByTokenQuery(string Token);

/// <summary>Command to accept a workspace invitation, creating the invited account if it does not yet exist.</summary>
public sealed record AcceptWorkspaceInvitationCommand(
    string Token,
    string? DisplayName,
    string? Password);

/// <summary>Command to decline a workspace invitation.</summary>
public sealed record DeclineWorkspaceInvitationCommand(string Token);

/// <summary>Command to cancel a pending workspace invitation.</summary>
public sealed record CancelWorkspaceInvitationCommand(
    Guid WorkspaceId,
    Guid InvitationId);

/// <summary>Represents a workspace along with the current user's role within it.</summary>
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    WorkspaceRole Role);

/// <summary>Represents a member of a workspace.</summary>
public sealed record WorkspaceMemberDto(
    Guid UserId,
    string DisplayName,
    string Email,
    WorkspaceRole Role);

/// <summary>Represents an invitation to join a workspace.</summary>
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
