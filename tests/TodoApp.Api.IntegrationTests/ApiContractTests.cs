using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class ApiContractTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Project_can_be_created_and_retrieved()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new
            {
                name = "API delivery",
                description = "Production HTTP milestone",
                targetDate = "2026-08-01"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var project = await response.Content
            .ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetGuid();

        Assert.Equal(
            $"/api/v1/projects/{projectId}",
            response.Headers.Location!.OriginalString);

        var retrieved = await _client.GetAsync(
            $"/api/v1/projects/{projectId}");

        Assert.Equal(HttpStatusCode.OK, retrieved.StatusCode);
        Assert.Equal(
            "application/json",
            retrieved.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Task_can_follow_create_to_complete_workflow()
    {
        var projectId = await CreateProjectAsync();
        var created = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new { title = "Ship versioned API", effort = 3 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = await created.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync(
                $"/api/v1/tasks/{taskId}/ready",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync(
                $"/api/v1/tasks/{taskId}/start",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync(
                $"/api/v1/tasks/{taskId}/complete",
                null)).StatusCode);

        var retrieved = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks/{taskId}");
        Assert.Equal("Completed", retrieved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Missing_resource_returns_problem_details()
    {
        var response = await _client.GetAsync(
            $"/api/v1/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_project_returns_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_details_instead_of_html()
    {
        var response = await _client.GetAsync("/api/v1/not-a-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new
            {
                name = $"Project {Guid.NewGuid():N}",
                targetDate = "2026-08-01"
            });
        response.EnsureSuccessStatusCode();
        var project = await response.Content
            .ReadFromJsonAsync<JsonElement>();
        return project.GetProperty("id").GetGuid();
    }
}
