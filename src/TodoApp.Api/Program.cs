using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TodoApp.Api;
using TodoApp.Api.Diagnostics;
using TodoApp.Api.Endpoints;
using TodoApp.Infrastructure;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

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

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();

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

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"))
    .ExcludeFromDescription();
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

app.Run();

public partial class Program;
