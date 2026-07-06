using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace TodoApp.Api.IntegrationTests;

public sealed class OperationalContractTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApi_document_describes_versioned_routes()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var document = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            document.GetProperty("paths")
                .TryGetProperty("/api/v1/projects", out _));
        Assert.True(
            document.GetProperty("paths")
                .TryGetProperty("/api/v1/tasks/{taskId}", out _));
    }

    [Fact]
    public async Task Root_serves_the_web_application()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"root\"></div>", html);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_available(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_on_response()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health/live");
        request.Headers.Add("X-Correlation-ID", "portfolio-test-123");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(
            "X-Correlation-ID",
            out var values));
        Assert.Equal("portfolio-test-123", Assert.Single(values));
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        using var content = new StringContent(
            """{"name":""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync(
            "/api/v1/projects",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_lifecycle_transition_returns_conflict()
    {
        var project = await CreateProjectAsync();
        var created = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{project}/tasks",
            new { title = "Cannot complete from backlog" });
        var task = await created.Content.ReadFromJsonAsync<JsonElement>();

        var response = await _client.PostAsync(
            $"/api/v1/tasks/{task.GetProperty("id").GetGuid()}/complete",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new { name = $"Operations {Guid.NewGuid():N}" });
        var project = await response.Content
            .ReadFromJsonAsync<JsonElement>();
        return project.GetProperty("id").GetGuid();
    }
}
