using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;
using Xunit;

namespace TodoApp.Domain.Tests.Collaboration;

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

    private static Workspace CreateWorkspace() =>
        Workspace.Create(Guid.NewGuid(), "Portfolio team", OwnerId);
}
