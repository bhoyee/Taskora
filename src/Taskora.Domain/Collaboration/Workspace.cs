using TodoApp.Domain.Common;

namespace TodoApp.Domain.Collaboration;

public sealed class Workspace
{
    private readonly List<WorkspaceMembership> _memberships = [];

    private Workspace()
    {
    }

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

    public IReadOnlyCollection<WorkspaceMembership> Memberships =>
        _memberships.AsReadOnly();

    public static Workspace Create(Guid id, string name, Guid ownerId) =>
        new(id, name, ownerId);

    public void Rename(Guid actorId, string name)
    {
        EnsureOwner(actorId);
        Name = NormalizeName(name);
    }

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

    public bool HasMember(Guid userId) =>
        _memberships.Any(member => member.UserId == userId);

    public WorkspaceRole GetRole(Guid userId) =>
        GetMembership(userId).Role;

    private WorkspaceMembership GetMembership(Guid userId) =>
        _memberships.SingleOrDefault(member => member.UserId == userId) ??
        throw new DomainRuleException(
            "The user does not belong to the workspace.");

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
