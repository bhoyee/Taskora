using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;
using Xunit;

namespace TodoApp.Domain.Tests.Collaboration;

// Tests for Workspace aggregate: membership/role management, owner protections, and suspension/reactivation.
public sealed class WorkspaceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_AddsCreatorAsOwner()
    {
        var workspace = Workspace.Create(
            Guid.NewGuid(),
            "Portfolio team",
            OwnerId);

        var owner = Assert.Single(workspace.Memberships);
        Assert.Equal(OwnerId, owner.UserId);
        Assert.Equal(WorkspaceRole.Owner, owner.Role);
    }

    [Fact]
    public void AddMember_WhenActorIsOwner_AddsRequestedRole()
    {
        var workspace = CreateWorkspace();
        var memberId = Guid.NewGuid();

        workspace.AddMember(OwnerId, memberId, WorkspaceRole.Manager);

        Assert.Contains(
            workspace.Memberships,
            member => member.UserId == memberId &&
                      member.Role == WorkspaceRole.Manager);
    }

    [Fact]
    public void AddMember_WhenActorIsNotOwner_IsRejected()
    {
        var workspace = CreateWorkspace();

        Assert.Throws<DomainRuleException>(
            () => workspace.AddMember(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkspaceRole.Member));
    }

    [Fact]
    public void AddMember_WhenUserAlreadyBelongs_IsRejected()
    {
        var workspace = CreateWorkspace();
        var memberId = Guid.NewGuid();
        workspace.AddMember(OwnerId, memberId, WorkspaceRole.Member);

        Assert.Throws<DomainRuleException>(
            () => workspace.AddMember(
                OwnerId,
                memberId,
                WorkspaceRole.Manager));
    }

    [Fact]
    public void ChangeRole_WhenTargetIsOwner_IsRejected()
    {
        var workspace = CreateWorkspace();

        Assert.Throws<DomainRuleException>(
            () => workspace.ChangeRole(
                OwnerId,
                OwnerId,
                WorkspaceRole.Member));
    }

    [Fact]
    public void RemoveMember_WhenTargetIsOwner_IsRejected()
    {
        var workspace = CreateWorkspace();

        Assert.Throws<DomainRuleException>(
            () => workspace.RemoveMember(OwnerId, OwnerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameIsBlank_IsRejected(string name)
    {
        Assert.Throws<DomainValidationException>(
            () => Workspace.Create(Guid.NewGuid(), name, OwnerId));
    }

    [Fact]
    public void Suspend_SetsSuspensionFields()
    {
        var workspace = CreateWorkspace();
        var adminId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        workspace.Suspend(adminId, "Non-payment", occurredAt);

        Assert.True(workspace.IsSuspended);
        Assert.Equal(occurredAt, workspace.SuspendedAt);
        Assert.Equal(adminId, workspace.SuspendedByUserId);
        Assert.Equal("Non-payment", workspace.SuspendedReason);
    }

    [Fact]
    public void Suspend_WhenReasonIsBlank_StoresNull()
    {
        var workspace = CreateWorkspace();

        workspace.Suspend(Guid.NewGuid(), "   ", DateTimeOffset.UtcNow);

        Assert.Null(workspace.SuspendedReason);
    }

    [Fact]
    public void Suspend_WhenActorIsEmpty_IsRejected()
    {
        var workspace = CreateWorkspace();

        Assert.Throws<DomainValidationException>(
            () => workspace.Suspend(Guid.Empty, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reactivate_ClearsSuspensionFields()
    {
        var workspace = CreateWorkspace();
        workspace.Suspend(Guid.NewGuid(), "Investigation", DateTimeOffset.UtcNow);

        workspace.Reactivate();

        Assert.False(workspace.IsSuspended);
        Assert.Null(workspace.SuspendedAt);
        Assert.Null(workspace.SuspendedByUserId);
        Assert.Null(workspace.SuspendedReason);
    }

    private static Workspace CreateWorkspace() =>
        Workspace.Create(Guid.NewGuid(), "Portfolio team", OwnerId);
}
