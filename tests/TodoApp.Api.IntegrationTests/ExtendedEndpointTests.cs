using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class ExtendedEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Project_can_be_updated_viewed_as_board_and_archived()
    {
        var projectId = await CreateProjectAsync();

        var updated = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}",
            new
            {
                name = "Renamed delivery",
                description = "Updated over HTTP",
                targetDate = "2026-09-01"
            });
        var board = await _client.GetAsync(
            $"/api/v1/projects/{projectId}/board");
        var archived = await _client.PostAsync(
            $"/api/v1/projects/{projectId}/archive",
            null);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var rejectedTask = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new { title = "Too late" });
        Assert.Equal(HttpStatusCode.Conflict, rejectedTask.StatusCode);
    }

    [Fact]
    public async Task Task_supports_planning_dependencies_and_full_maintenance()
    {
        var projectId = await CreateProjectAsync();
        var dependencyId = await CreateTaskAsync(
            projectId,
            "Dependency");
        var taskId = await CreateTaskAsync(projectId, "Main task");

        AssertSuccess(await _client.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new
            {
                title = "Updated main task",
                dueDate = "2026-08-15",
                effort = 5
            }));
        AssertSuccess(await _client.PutAsJsonAsync(
            $"/api/v1/tasks/{taskId}/planning",
            new
            {
                businessValue = 5,
                urgency = 4,
                riskReduction = 3,
                effort = 5
            }));
        AssertSuccess(await _client.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/dependencies",
            new { dependencyId }));
        AssertSuccess(await _client.DeleteAsync(
            $"/api/v1/tasks/{taskId}/dependencies/{dependencyId}"));

        AssertSuccess(await PostAsync(taskId, "ready"));
        AssertSuccess(await PostAsync(taskId, "start"));
        AssertSuccess(await _client.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/block",
            new { reason = "Review required" }));
        AssertSuccess(await PostAsync(taskId, "unblock"));
        AssertSuccess(await PostAsync(taskId, "start"));
        AssertSuccess(await PostAsync(taskId, "complete"));
        AssertSuccess(await PostAsync(taskId, "reopen"));

        var activity = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks/{taskId}/activity");
        Assert.True(activity.GetArrayLength() >= 6);
        Assert.Equal(
            "system",
            activity[0].GetProperty("actor").GetString());
        Assert.Equal(
            "StatusChanged",
            activity[0].GetProperty("action").GetString());

        var search = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks?projectId={projectId}&search=Updated&pageNumber=1&pageSize=10");

        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            "Updated main task",
            search.GetProperty("items")[0]
                .GetProperty("title")
                .GetString());
        Assert.True(
            search.GetProperty("items")[0]
                .GetProperty("priorityExplanation")
                .GetProperty("businessValueContribution")
                .GetInt32() > 0);
        Assert.Equal(
            "Healthy",
            search.GetProperty("items")[0]
                .GetProperty("deadlineHealth")
                .GetString());

        var dashboard = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/dashboard");
        Assert.True(dashboard.GetProperty("projectCount").GetInt32() > 0);
        Assert.True(dashboard.GetProperty("activeTaskCount").GetInt32() > 0);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new
            {
                name = $"Extended {Guid.NewGuid():N}",
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

    private Task<HttpResponseMessage> PostAsync(
        Guid taskId,
        string action) =>
        _client.PostAsync(
            $"/api/v1/tasks/{taskId}/{action}",
            null);

    private static void AssertSuccess(HttpResponseMessage response) =>
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but received {(int)response.StatusCode}.");
}
