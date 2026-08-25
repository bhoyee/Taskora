using TodoApp.Domain.Common;

namespace TodoApp.Domain.Collaboration;

/// <summary>
/// Aggregate root for a workspace: a named container owned by a single user who has
/// exclusive authority to manage its membership. Enforces that there is always exactly
/// one owner and that only the owner can add, remove, or re-role members.
/// </summary>
public sealed class Workspace
{
    private readonly List<WorkspaceMembership> _memberships = [];

    // Reserved for ORM materialization; domain code must use the factory method.
    private Workspace()
    {
    }

    // Creating a workspace immediately grants the owner an Owner membership so the
    // aggregate never exists without a valid owner in its membership list.
    private Workspace(Guid id, string name, Guid ownerId)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "Workspace identifier is required.");
        }

        if (ownerId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Workspace owner is required.");
        }

        Id = id;
        Name = NormalizeName(name);
        OwnerId = ownerId;
        _memberships.Add(
            new WorkspaceMembership(id, ownerId, WorkspaceRole.Owner));
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public bool IsSuspended => SuspendedAt.HasValue;

    public DateTimeOffset? SuspendedAt { get; private set; }

    public Guid? SuspendedByUserId { get; private set; }

    public string? SuspendedReason { get; private set; }

    public IReadOnlyCollection<WorkspaceMembership> Memberships =>
        _memberships.AsReadOnly();

    /// <summary>Creates a new workspace, automatically enrolling <paramref name="ownerId"/> as its owner.</summary>
    public static Workspace Create(Guid id, string name, Guid ownerId) =>
        new(id, name, ownerId);

    /// <summary>Renames the workspace. Only the owner may perform this action.</summary>
    public void Rename(Guid actorId, string name)
    {
        EnsureOwner(actorId);
        Name = NormalizeName(name);
    }

    /// <summary>
    /// Adds a new member to the workspace. Only the owner may add members, a role of
    /// <see cref="WorkspaceRole.Owner"/> is rejected (there is only ever one owner), and
    /// a user already in the workspace cannot be added again.
    /// </summary>
    public void AddMember(
        Guid actorId,
        Guid userId,
        WorkspaceRole role)
    {
        EnsureOwner(actorId);

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User identifier is required.");
        }

        if (role == WorkspaceRole.Owner)
        {
            throw new DomainRuleException(
                "A workspace can only have one owner.");
        }

        if (_memberships.Any(member => member.UserId == userId))
        {
            throw new DomainRuleException(
                "The user already belongs to the workspace.");
        }

        _memberships.Add(new WorkspaceMembership(Id, userId, role));
    }

    /// <summary>
    /// Changes a member's role. Only the owner may do this, and the owner's own
    /// membership can never be changed (neither reassigning it away from Owner nor
    /// promoting someone else to Owner through this method).
    /// </summary>
    public void ChangeRole(
        Guid actorId,
        Guid userId,
        WorkspaceRole role)
    {
        EnsureOwner(actorId);
        var membership = GetMembership(userId);

        if (userId == OwnerId || role == WorkspaceRole.Owner)
        {
            throw new DomainRuleException(
                "The owner membership cannot be changed.");
        }

        membership.ChangeRole(role);
    }

    /// <summary>Removes a member. Only the owner may do this, and the owner cannot remove themselves.</summary>
    public void RemoveMember(Guid actorId, Guid userId)
    {
        EnsureOwner(actorId);

        if (userId == OwnerId)
        {
            throw new DomainRuleException(
                "The workspace owner cannot be removed.");
        }

        _memberships.Remove(GetMembership(userId));
    }

    /// <summary>
    /// Marks the workspace suspended (e.g. by an administrator), recording who suspended
    /// it, when, and an optional reason.
    /// </summary>
    public void Suspend(
        Guid suspendedByUserId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        if (suspendedByUserId == Guid.Empty)
        {
            throw new DomainValidationException(
                "Suspending user identifier is required.");
        }

        SuspendedAt = occurredAt;
        SuspendedByUserId = suspendedByUserId;
        SuspendedReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();
    }

    /// <summary>Clears any suspension, restoring the workspace to active status.</summary>
    public void Reactivate()
    {
        SuspendedAt = null;
        SuspendedByUserId = null;
        SuspendedReason = null;
    }

    public bool HasMember(Guid userId) =>
        _memberships.Any(member => member.UserId == userId);

    public WorkspaceRole GetRole(Guid userId) =>
        GetMembership(userId).Role;

    // Looks up a member's membership record, throwing a domain rule violation if they
    // are not actually part of this workspace.
    private WorkspaceMembership GetMembership(Guid userId) =>
        _memberships.SingleOrDefault(member => member.UserId == userId) ??
        throw new DomainRuleException(
            "The user does not belong to the workspace.");

    // Central authorization guard: membership management is restricted to the owner.
    private void EnsureOwner(Guid actorId)
    {
        if (actorId != OwnerId)
        {
            throw new DomainRuleException(
                "Only the workspace owner can manage membership.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Workspace name is required.");
        }

        return name.Trim();
    }
}
