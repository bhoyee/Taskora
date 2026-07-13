using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class PersonalTodoEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Personal_todos_support_crud_for_signed_in_user()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/v1/todos",
            new
            {
                title = "Review portfolio notes",
                todoDate = "2026-07-13",
                notes = "Keep it short"
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var todoId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal(
            "Review portfolio notes",
            createdJson.GetProperty("title").GetString());

        var list = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/todos?date=2026-07-13");
        Assert.Single(list.EnumerateArray());

        var updated = await _client.PutAsJsonAsync(
            $"/api/v1/todos/{todoId}",
            new
            {
                title = "Review portfolio notes again",
                todoDate = "2026-07-13",
                notes = "Add screenshots"
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var completed = await _client.PostAsync(
            $"/api/v1/todos/{todoId}/complete",
            null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.True(
            (await completed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("isCompleted")
            .GetBoolean());

        var reopened = await _client.PostAsync(
            $"/api/v1/todos/{todoId}/reopen",
            null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.False(
            (await reopened.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("isCompleted")
            .GetBoolean());

        var deleted = await _client.DeleteAsync($"/api/v1/todos/{todoId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var empty = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/todos?date=2026-07-13");
        Assert.Empty(empty.EnumerateArray());
    }
}
