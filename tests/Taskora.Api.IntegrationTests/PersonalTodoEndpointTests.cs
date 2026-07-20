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
        var marker = $"crud-{Guid.NewGuid():N}";
        var title = $"Review portfolio notes {marker}";
        var created = await _client.PostAsJsonAsync(
            "/api/v1/todos",
            new
            {
                title,
                todoDate = "2026-07-13",
                notes = "Keep it short"
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var todoId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal(
            title,
            createdJson.GetProperty("title").GetString());
        Assert.Equal(
            "Medium",
            createdJson.GetProperty("priority").GetString());

        var list = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/todos?date=2026-07-13&search={marker}&pageNumber=1&pageSize=10");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Single(list.GetProperty("items").EnumerateArray());

        var updated = await _client.PutAsJsonAsync(
            $"/api/v1/todos/{todoId}",
            new
            {
                title = $"Review portfolio notes again {marker}",
                todoDate = "2026-07-13",
                notes = "Add screenshots",
                priority = "High"
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(
            "High",
            (await updated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("priority")
            .GetString());

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
            $"/api/v1/todos?date=2026-07-13&search={marker}");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Incomplete_todos_are_not_carried_over_to_future_selected_date()
    {
        var marker = $"carry-{Guid.NewGuid():N}";
        var title = $"Carry this forward {marker}";
        var created = await _client.PostAsJsonAsync(
            "/api/v1/todos",
            new
            {
                title,
                todoDate = "2099-01-01",
                notes = "Still open"
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var futureList = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/todos?date=2099-01-02&search={marker}&pageNumber=1&pageSize=10");
        Assert.Equal(0, futureList.GetProperty("totalCount").GetInt32());
        Assert.Empty(futureList.GetProperty("items").EnumerateArray());

        var originalDateList = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/todos?date=2099-01-01&search={marker}&pageNumber=1&pageSize=10");
        var item = originalDateList.GetProperty("items")[0];

        Assert.Equal(1, originalDateList.GetProperty("totalCount").GetInt32());
        Assert.Equal(title, item.GetProperty("title").GetString());
        Assert.Equal("2099-01-01", item.GetProperty("todoDate").GetString());
        Assert.Equal("2099-01-01", item.GetProperty("originalTodoDate").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("carriedOverFromDate").ValueKind);
    }

    [Fact]
    public async Task Daily_routines_support_crud_and_generate_high_priority_todos()
    {
        var marker = $"routine-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync(
            "/api/v1/todos/routines",
            new
            {
                title = $"Review delivery board {marker}",
                notes = "Check blockers first.",
                startDate = "2026-07-20"
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var routineId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal("High", createdJson.GetProperty("priority").GetString());
        Assert.True(createdJson.GetProperty("isActive").GetBoolean());

        var list = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/todos/routines?pageNumber=1&pageSize=10");
        Assert.Contains(
            list.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == routineId);

        var updated = await _client.PutAsJsonAsync(
            $"/api/v1/todos/routines/{routineId}",
            new
            {
                title = $"Review delivery board updated {marker}",
                notes = "Check release risks first.",
                priority = "Critical",
                startDate = "2026-07-20",
                endDate = "2026-07-25",
                isActive = true
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(
            "Critical",
            (await updated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("priority")
            .GetString());

        var today = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/todos?date=2026-07-20&search={marker}&pageNumber=1&pageSize=10");
        var generated = Assert.Single(today.GetProperty("items").EnumerateArray());
        Assert.Equal("Critical", generated.GetProperty("priority").GetString());
        Assert.Equal(routineId, generated.GetProperty("dailyRoutineId").GetGuid());
        Assert.True(generated.GetProperty("isGeneratedFromDailyRoutine").GetBoolean());

        var deleted = await _client.DeleteAsync(
            $"/api/v1/todos/routines/{routineId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }
}
