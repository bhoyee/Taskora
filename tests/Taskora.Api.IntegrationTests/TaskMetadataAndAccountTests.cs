using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class TaskMetadataAndAccountTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Seeded_development_owner_can_login()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new
            {
                email = "salisu.adeboye@gmail.com",
                password = "Portfolio123!"
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "salisu.adeboye@gmail.com",
            session.GetProperty("email").GetString());
        Assert.Equal(
            "30000000-0000-0000-0000-000000000001",
            session.GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task Account_profile_and_password_can_be_updated()
    {
        var email = $"profile-{Guid.NewGuid():N}@example.com";
        var registered = await _client.PostAsJsonAsync(
            "/api/v1/account/register",
            new
            {
                displayName = "Profile Owner",
                email,
                password = "SecurePass123!",
                workspaceName = "Profile workspace"
            });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var registration = await registered.Content
            .ReadFromJsonAsync<JsonElement>();

        using var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Remove("X-User-Id");
        authenticated.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                registration.GetProperty("accessToken").GetString());

        var updatedEmail = $"updated-{Guid.NewGuid():N}@example.com";
        var profileResponse = await authenticated.PutAsJsonAsync(
            "/api/v1/account/profile",
            new { email = updatedEmail });

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profile = await profileResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Profile Owner", profile.GetProperty("displayName").GetString());
        Assert.Equal(updatedEmail, profile.GetProperty("email").GetString());

        var passwordResponse = await authenticated.PutAsJsonAsync(
            "/api/v1/account/password",
            new
            {
                currentPassword = "SecurePass123!",
                newPassword = "SecurePass456!"
            });
        Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);

        var oldLogin = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new { email = updatedEmail, password = "SecurePass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new { email = updatedEmail, password = "SecurePass456!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Account_can_register_and_login_with_bearer_token()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var registered = await _client.PostAsJsonAsync(
            "/api/v1/account/register",
            new
            {
                displayName = "Portfolio Owner",
                email,
                password = "SecurePass123!",
                workspaceName = "Portfolio workspace"
            });

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var registration = await registered.Content
            .ReadFromJsonAsync<JsonElement>();
        var accessToken = registration.GetProperty("accessToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.Equal(
            "portfolio owner",
            registration.GetProperty("displayName").GetString()!.ToLowerInvariant());

        var login = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new
            {
                email,
                password = "SecurePass123!"
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(accessToken, session.GetProperty("accessToken").GetString());

        using var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Remove("X-User-Id");
        authenticated.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var project = await authenticated.PostAsJsonAsync(
            "/api/v1/projects",
            new
            {
                name = $"Bearer project {Guid.NewGuid():N}",
                targetDate = "2026-08-01"
            });

        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
    }

    [Fact]
    public async Task Account_password_can_be_reset_with_emailed_code()
    {
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        var originalPassword = "SecurePass123!";
        var newPassword = "SecurePass456!";
        var registered = await _client.PostAsJsonAsync(
            "/api/v1/account/register",
            new
            {
                displayName = "Reset Owner",
                email,
                password = originalPassword,
                workspaceName = "Reset workspace"
            });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var request = await _client.PostAsJsonAsync(
            "/api/v1/account/password/reset/request",
            new { email });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var token = ReadLatestResetToken(email);
        Assert.Matches("^[0-9]{6}$", token);

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/account/password/reset/confirm",
            new
            {
                email,
                token,
                newPassword
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new { email, password = originalPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await _client.PostAsJsonAsync(
            "/api/v1/account/login",
            new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var reusedToken = await _client.PostAsJsonAsync(
            "/api/v1/account/password/reset/confirm",
            new
            {
                email,
                token,
                newPassword = "SecurePass789!"
            });
        Assert.Equal(HttpStatusCode.BadRequest, reusedToken.StatusCode);
    }

    [Fact]
    public async Task Task_metadata_endpoints_manage_category_tags_and_notes()
    {
        var projectId = await CreateProjectAsync();
        var categoryResponse = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/categories",
            new { name = "Client Work" });
        AssertSuccess(categoryResponse);
        var category = await categoryResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("id").GetGuid();

        var taskId = await CreateTaskAsync(projectId, "Prepare client plan");

        AssertSuccess(await _client.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}/category",
            new { categoryId }));
        AssertSuccess(await _client.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/tags",
            new { tag = "client" }));
        AssertSuccess(await _client.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/tags",
            new { tag = "planning" }));
        AssertSuccess(await _client.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/notes",
            new { body = "Confirmed stakeholder review window." }));

        var details = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks/{taskId}");

        Assert.Equal(categoryId, details.GetProperty("categoryId").GetGuid());
        Assert.Contains(
            details.GetProperty("tags").EnumerateArray(),
            tag => tag.GetString() == "client");
        Assert.Equal(
            "Confirmed stakeholder review window.",
            details.GetProperty("notes")[0].GetProperty("body").GetString());

        var filtered = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks?projectId={projectId}&categoryId={categoryId}&tag=client&pageNumber=1&pageSize=10");

        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            taskId,
            filtered.GetProperty("items")[0].GetProperty("id").GetGuid());

        AssertSuccess(await _client.DeleteAsync(
            $"/api/v1/tasks/{taskId}/tags/client"));
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new
            {
                name = $"Metadata {Guid.NewGuid():N}",
                targetDate = "2026-08-01"
            });
        AssertSuccess(response);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(
        Guid projectId,
        string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new { title });
        AssertSuccess(response);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetGuid();
    }

    private string ReadLatestResetToken(string email)
    {
        var resetEmail = factory.EmailSender.Messages.Last(message =>
            message.Recipients.Contains(email) &&
            message.Subject.Contains(
                "password reset",
                StringComparison.OrdinalIgnoreCase));
        var message = resetEmail.Body;
        var match = Regex.Match(message, @"reset code is:\s*(\d{6})");

        Assert.True(match.Success, "Expected the reset email log to contain a 6-digit code.");
        return match.Groups[1].Value;
    }

    private static void AssertSuccess(HttpResponseMessage response) =>
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but received {(int)response.StatusCode}.");
}
