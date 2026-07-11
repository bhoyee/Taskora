using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TodoApp.Api;
using TodoApp.Api.Diagnostics;
using TodoApp.Api.Endpoints;
using TodoApp.Api.Security;
using TodoApp.Infrastructure;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

LoadEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()));
builder.Services.AddHealthChecks()
    .AddCheck(
        "process",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddDbContextCheck<TodoAppDbContext>(
        "database",
        tags: ["ready"]);
builder.Services.AddApplicationUseCases();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTodoSecurity(
    builder.Environment,
    builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider
        .GetRequiredService<TodoAppDbContext>();
    await database.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(
        database,
        CancellationToken.None);
}

app.MapStaticAssets();
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
app.MapProjectEndpoints();
app.MapTaskEndpoints();
app.MapIntelligenceEndpoints();
app.MapNotificationEndpoints();
app.MapWorkspaceEndpoints();
app.MapAccountEndpoints();
app.Map("/api/{**path}", () => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "API endpoint not found."));
var publishedIndex = Path.Combine(
    app.Environment.ContentRootPath,
    "wwwroot",
    "index.html");
var developmentIndex = Path.GetFullPath(
    Path.Combine(
        app.Environment.ContentRootPath,
        "..",
        "TodoApp.Web",
        "dist",
        "index.html"));
var webIndex = File.Exists(publishedIndex)
    ? publishedIndex
    : developmentIndex;
app.MapFallback(() => Results.File(webIndex, "text/html"));

app.Run();

static void LoadEnvironmentFile()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var path = Path.Combine(directory.FullName, ".env");
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = trimmed[..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }

        directory = directory.Parent;
    }
}

public partial class Program;
