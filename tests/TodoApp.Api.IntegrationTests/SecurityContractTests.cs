using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Domain.Collaboration;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class SecurityContractTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private static readonly Guid OwnerId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberId =
        Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid ManagerId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid WorkspaceId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Workspaces_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient()
            .GetAsync("/api/v1/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Workspaces_ReturnOnlyAuthenticatedUsersMemberships()
    {
        using var client = CreateClient(MemberId);

        var workspaces = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/workspaces");

        var workspace = Assert.Single(workspaces.EnumerateArray());
        Assert.Equal(WorkspaceId, workspace.GetProperty("id").GetGuid());
        Assert.Equal("Member", workspace.GetProperty("role").GetString());
    }

    [Fact]
    public async Task MembershipChange_WhenActorIsMember_ReturnsForbidden()
    {
        using var client = CreateClient(MemberId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{WorkspaceId}/members",
            new
            {
                email = "manager@example.com",
                role = WorkspaceRole.Manager
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Members_WhenActorIsOwner_ReturnsProfilesAndRoles()
    {
        using var client = CreateClient(OwnerId);

        var members = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/workspaces/{WorkspaceId}/members");

        Assert.Equal(3, members.GetArrayLength());
        Assert.Contains(
            members.EnumerateArray(),
            member =>
                member.GetProperty("email").GetString() ==
                "manager@example.com");
    }

    [Fact]
    public async Task Assignment_WhenActorIsManager_CanAssignWorkspaceMember()
    {
        using var client = CreateClient(ManagerId);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{TaskId}/assignment",
            new { userId = MemberId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks/{TaskId}");
        Assert.Equal(
            MemberId,
            task.GetProperty("assignedUserId").GetGuid());
    }

    [Fact]
    public async Task Assignment_WhenActorIsMember_ReturnsForbidden()
    {
        using var client = CreateClient(MemberId);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{TaskId}/assignment",
            new { userId = ManagerId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }
}
