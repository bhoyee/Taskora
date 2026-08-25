using TodoApp.Application.Abstractions;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Common;
using TodoApp.Domain.Collaboration;

namespace TodoApp.Application.Tests.Collaboration;

// Covers workspace management handlers: rename, delete (including the
// "can't delete your last workspace" rule and owner-only authorization),
// suspend/reactivate, and member listing with suspension enforcement,
// plus the administrative bypass flag that overrides these checks.
public sealed class WorkspaceManagementHandlerTests
{
    private static readonly Guid OwnerId =
        Guid.Parse("efaf3ba7-4c81-4fbf-985d-e79eaf10d4cf");
    private static readonly Guid ManagerId =
        Guid.Parse("895c94c4-88c9-4715-a9ba-740b18783736");
    private static readonly Guid WorkspaceId =
        Guid.Parse("43823da4-50e1-4c0e-b69e-a83f2d353a7c");
    private static readonly Guid OtherWorkspaceId =
        Guid.Parse("d2188e76-6b0c-44c9-a322-f52fa35459ef");

    [Fact]
    public async Task UpdateWorkspace_WhenOwnerRenamesWorkspace_SavesChange()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new UpdateWorkspaceHandler(
            new InMemoryWorkspaceRepository(workspace),
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new UpdateWorkspaceCommand(WorkspaceId, "Delivery team"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Delivery team", result.Value.Name);
        Assert.Equal("Delivery team", workspace.Name);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeleteWorkspace_WhenUserHasAnotherWorkspace_RemovesWorkspace()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var otherWorkspace = Workspace.Create(
            OtherWorkspaceId,
            "Client delivery",
            OwnerId);
        var repository = new InMemoryWorkspaceRepository(
            workspace,
            otherWorkspace);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteWorkspaceHandler(
            repository,
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(WorkspaceId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(WorkspaceId, repository.RemovedWorkspace?.Id);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeleteWorkspace_WhenOnlyWorkspace_ReturnsConflict()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteWorkspaceHandler(
            repository,
            unitOfWork,
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(WorkspaceId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("workspace.last_workspace", result.Error.Code);
        Assert.Null(repository.RemovedWorkspace);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeleteWorkspace_WhenUserIsManager_ReturnsForbidden()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.AddMember(OwnerId, ManagerId, WorkspaceRole.Manager);
        var otherWorkspace = Workspace.Create(
            OtherWorkspaceId,
            "Client delivery",
            ManagerId);
        var repository = new InMemoryWorkspaceRepository(
            workspace,
            otherWorkspace);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteWorkspaceHandler(
            repository,
            unitOfWork,
            new StubCurrentUser(ManagerId));

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(WorkspaceId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Null(repository.RemovedWorkspace);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    // The administrative bypass flag lets the deletion proceed even though
    // the caller isn't a member and it's the workspace's only remaining one,
    // both of which would otherwise block the deletion.
    [Fact]
    public async Task DeleteWorkspace_WhenBypassAndOnlyWorkspace_RemovesWorkspace()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new DeleteWorkspaceHandler(
            repository,
            unitOfWork,
            new StubCurrentUser(Guid.NewGuid()));

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(WorkspaceId, HasAdministrativeBypass: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkspaceId, repository.RemovedWorkspace?.Id);
    }

    [Fact]
    public async Task SuspendWorkspace_SetsSuspensionAndSaves()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var unitOfWork = new RecordingUnitOfWork();
        var adminId = Guid.NewGuid();
        var handler = new SuspendWorkspaceHandler(
            repository,
            unitOfWork,
            new StubClock(DateTimeOffset.UtcNow),
            new StubCurrentUser(adminId));

        var result = await handler.HandleAsync(
            new SuspendWorkspaceCommand(WorkspaceId, "Non-payment"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(workspace.IsSuspended);
        Assert.Equal(adminId, workspace.SuspendedByUserId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReactivateWorkspace_ClearsSuspension()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.Suspend(Guid.NewGuid(), "Investigation", DateTimeOffset.UtcNow);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var unitOfWork = new RecordingUnitOfWork();
        var handler = new ReactivateWorkspaceHandler(repository, unitOfWork);

        var result = await handler.HandleAsync(
            new ReactivateWorkspaceCommand(WorkspaceId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(workspace.IsSuspended);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetWorkspaceMembers_WhenWorkspaceIsSuspended_ReturnsNotFoundForMember()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.Suspend(Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var handler = new GetWorkspaceMembersHandler(
            repository,
            new StubUserProfileRepository(),
            new StubCurrentUser(OwnerId));

        var result = await handler.HandleAsync(
            new GetWorkspaceMembersQuery(WorkspaceId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    // The administrative bypass flag lets a non-member read members of a
    // suspended workspace, which would otherwise return NotFound.
    [Fact]
    public async Task GetWorkspaceMembers_WhenSuspendedButBypassed_Succeeds()
    {
        var workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
        workspace.Suspend(Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var repository = new InMemoryWorkspaceRepository(workspace);
        var handler = new GetWorkspaceMembersHandler(
            repository,
            new StubUserProfileRepository(),
            new StubCurrentUser(Guid.NewGuid()));

        var result = await handler.HandleAsync(
            new GetWorkspaceMembersQuery(WorkspaceId, HasAdministrativeBypass: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    // IClock stub returning a fixed point in time for deterministic tests.
    private sealed class StubClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    // IUserProfileRepository stub that synthesizes a placeholder profile
    // for every requested user id.
    private sealed class StubUserProfileRepository : IUserProfileRepository
    {
        public Task<UserProfile?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserProfile?>(null);

        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserProfile>>(
                userIds
                    .Select(id => UserProfile.Create(id, "Workspace Owner", $"{id}@example.com"))
                    .ToArray());
    }

    // In-memory IWorkspaceRepository fake that also records the last
    // removed workspace for assertions.
    private sealed class InMemoryWorkspaceRepository(params Workspace[] workspaces)
        : IWorkspaceRepository
    {
        private readonly Dictionary<Guid, Workspace> _workspaces =
            workspaces.ToDictionary(workspace => workspace.Id);

        public Workspace? RemovedWorkspace { get; private set; }

        public Task AddAsync(
            Workspace workspace,
            CancellationToken cancellationToken)
        {
            _workspaces.Add(workspace.Id, workspace);
            return Task.CompletedTask;
        }

        public Task<Workspace?> GetByIdAsync(
            Guid workspaceId,
            CancellationToken cancellationToken)
        {
            _workspaces.TryGetValue(workspaceId, out var workspace);
            return Task.FromResult(workspace);
        }

        public Task RemoveAsync(
            Workspace workspace,
            CancellationToken cancellationToken)
        {
            RemovedWorkspace = workspace;
            _workspaces.Remove(workspace.Id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>(
                _workspaces.Values
                    .Where(workspace => workspace.HasMember(userId))
                    .ToArray());
    }

    // IUnitOfWork fake that counts how many times changes were saved.
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    // ICurrentUser stub representing a fixed, always-authenticated user.
    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId { get; } = userId;
    }
}
