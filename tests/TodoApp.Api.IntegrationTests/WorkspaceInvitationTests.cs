using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TodoApp.Domain.Collaboration;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class WorkspaceInvitationTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private static readonly Guid OwnerId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkspaceId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Owner_can_invite_external_person_who_accepts_and_logs_in()
    {
        using var owner = CreateDevelopmentUserClient(OwnerId);
        var email = $"invitee-{Guid.NewGuid():N}@example.com";

        var invitation = await InviteAsync(owner, email);
        var token = ExtractToken(invitation.GetProperty("inviteLink").GetString());

        using var anonymous = CreateAnonymousClient();
        var publicInvitation = await anonymous.GetFromJsonAsync<JsonElement>(
            $"/api/v1/invitations/{token}");

        Assert.Equal(
            "Portfolio team",
            publicInvitation.GetProperty("workspaceName").GetString());
        Assert.Equal(email, publicInvitation.GetProperty("email").GetString());

        var accept = await anonymous.PostAsJsonAsync(
            $"/api/v1/invitations/{token}/accept",
            new
            {
                displayName = "Invited Teammate",
                password = "SecurePass123!"
            });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var login = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login",
            new
            {
                email,
                password = "SecurePass123!"
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();

        using var invitee = CreateBearerClient(
            session.GetProperty("accessToken").GetString()!);
        var workspaces = await invitee.GetFromJsonAsync<JsonElement>(
            "/api/v1/workspaces");

        Assert.Contains(
            workspaces.EnumerateArray(),
            workspace =>
                workspace.GetProperty("id").GetGuid() == WorkspaceId &&
                workspace.GetProperty("role").GetString() == "Member");
    }

    [Fact]
    public async Task Removed_member_can_no_longer_access_workspace_data()
    {
        using var owner = CreateDevelopmentUserClient(OwnerId);
        var email = $"removed-{Guid.NewGuid():N}@example.com";
        var invitation = await InviteAsync(owner, email);
        var token = ExtractToken(invitation.GetProperty("inviteLink").GetString());

        using var anonymous = CreateAnonymousClient();
        var accept = await anonymous.PostAsJsonAsync(
            $"/api/v1/invitations/{token}/accept",
            new
            {
                displayName = "Removed Member",
                password = "SecurePass123!"
            });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var login = await anonymous.PostAsJsonAsync(
            "/api/v1/account/login",
            new
            {
                email,
                password = "SecurePass123!"
            });
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        var userId = session.GetProperty("userId").GetGuid();
        var accessToken = session.GetProperty("accessToken").GetString()!;

        var removal = await owner.DeleteAsync(
            $"/api/v1/workspaces/{WorkspaceId}/members/{userId}");
        Assert.Equal(HttpStatusCode.OK, removal.StatusCode);

        using var removed = CreateBearerClient(accessToken);
        var workspaces = await removed.GetFromJsonAsync<JsonElement>(
            "/api/v1/workspaces");
        Assert.Empty(workspaces.EnumerateArray());

        var members = await removed.GetAsync(
            $"/api/v1/workspaces/{WorkspaceId}/members");
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
    }

    [Fact]
    public async Task Pending_invitation_can_be_declined_or_cancelled()
    {
        using var owner = CreateDevelopmentUserClient(OwnerId);
        var declined = await InviteAsync(
            owner,
            $"declined-{Guid.NewGuid():N}@example.com");
        var declinedToken = ExtractToken(
            declined.GetProperty("inviteLink").GetString());

        using var anonymous = CreateAnonymousClient();
        var decline = await anonymous.PostAsync(
            $"/api/v1/invitations/{declinedToken}/decline",
            content: null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        var cancelled = await InviteAsync(
            owner,
            $"cancelled-{Guid.NewGuid():N}@example.com");
        var cancel = await owner.DeleteAsync(
            $"/api/v1/workspaces/{WorkspaceId}/invitations/{cancelled.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }

    private async Task<JsonElement> InviteAsync(
        HttpClient owner,
        string email)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/v1/workspaces/{WorkspaceId}/invitations",
            new
            {
                fullName = "Invited Teammate",
                email,
                role = WorkspaceRole.Member
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private HttpClient CreateDevelopmentUserClient(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private HttpClient CreateAnonymousClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-User-Id");
        return client;
    }

    private HttpClient CreateBearerClient(string token)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string ExtractToken(string? inviteLink)
    {
        Assert.False(string.IsNullOrWhiteSpace(inviteLink));
        return inviteLink.Split('/').Last();
    }
}
