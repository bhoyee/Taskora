namespace TodoApp.Domain.Collaboration;

/// <summary>
/// Lifecycle states of an invitation to join a workspace.
/// </summary>
public enum WorkspaceInvitationStatus
{
    /// <summary>Invitation has been sent but not yet acted on by the invitee.</summary>
    Pending = 0,

    /// <summary>Invitee accepted and became a workspace member.</summary>
    Accepted = 1,

    /// <summary>Invitee explicitly declined the invitation.</summary>
    Declined = 2,

    /// <summary>Invitation was withdrawn by the inviter/workspace before a response.</summary>
    Cancelled = 3,

    /// <summary>Invitation was not acted on within its validity window and lapsed.</summary>
    Expired = 4
}
