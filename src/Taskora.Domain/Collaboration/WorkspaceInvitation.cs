using TodoApp.Domain.Common;

namespace TodoApp.Domain.Collaboration;

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation()
    {
    }

    private WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        string fullName,
        string email,
        WorkspaceRole role,
        Guid invitedByUserId,
        string token,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Invitation identifier is required.");
        }

        if (workspaceId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Workspace identifier is required.");
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Inviting user is required.");
        }

        if (role == WorkspaceRole.Owner)
        {
            throw new DomainRuleException(
                "Workspace invitations cannot grant owner role.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        FullName = NormalizeRequired(fullName, "Invitee full name is required.");
        Email = NormalizeEmail(email);
        Role = role;
        InvitedByUserId = invitedByUserId;
        Token = NormalizeRequired(token, "Invitation token is required.");
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = WorkspaceInvitationStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public WorkspaceRole Role { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public WorkspaceInvitationStatus Status { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsPending(DateTimeOffset now) =>
        Status == WorkspaceInvitationStatus.Pending && ExpiresAt > now;

    public static WorkspaceInvitation Create(
        Guid id,
        Guid workspaceId,
        string fullName,
        string email,
        WorkspaceRole role,
        Guid invitedByUserId,
        string token,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new(
            id,
            workspaceId,
            fullName,
            email,
            role,
            invitedByUserId,
            token,
            createdAt,
            expiresAt);

    public void Accept(DateTimeOffset acceptedAt)
    {
        EnsurePending(acceptedAt);
        Status = WorkspaceInvitationStatus.Accepted;
        RespondedAt = acceptedAt;
    }

    public void Decline(DateTimeOffset declinedAt)
    {
        EnsurePending(declinedAt);
        Status = WorkspaceInvitationStatus.Declined;
        RespondedAt = declinedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        EnsurePending(cancelledAt);
        Status = WorkspaceInvitationStatus.Cancelled;
        RespondedAt = cancelledAt;
    }

    public void MarkExpired(DateTimeOffset expiredAt)
    {
        if (Status == WorkspaceInvitationStatus.Pending &&
            ExpiresAt <= expiredAt)
        {
            Status = WorkspaceInvitationStatus.Expired;
            RespondedAt = expiredAt;
        }
    }

    private void EnsurePending(DateTimeOffset now)
    {
        MarkExpired(now);

        if (Status != WorkspaceInvitationStatus.Pending)
        {
            throw new DomainRuleException(
                "The workspace invitation is no longer pending.");
        }
    }

    private static string NormalizeRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(message);
        }

        return value.Trim();
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = NormalizeRequired(
            email,
            "Invitee email is required.").ToLowerInvariant();
        if (!normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                "A valid invitee email is required.");
        }

        return normalized;
    }
}
