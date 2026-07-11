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

ValidateDeploymentConfiguration(
    builder.Environment,
    builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ConfiguredFrontend",
        policy =>
        {
            var origins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            if (origins.Length == 0 && builder.Environment.IsDevelopment())
            {
                origins = ["http://localhost:5173"];
            }

            if (origins.Length > 0)
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
});
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
app.UseCors("ConfiguredFrontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (ShouldApplyMigrations(app.Environment, app.Configuration))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var logger = migrationScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var database = migrationScope.ServiceProvider
        .GetRequiredService<TodoAppDbContext>();
    logger.LogInformation("Applying EF Core database migrations.");
    await database.Database.MigrateAsync();
}

if (ShouldSeedDemoData(app.Environment, app.Configuration))
{
    await using var seedScope = app.Services.CreateAsyncScope();
    var logger = seedScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var database = seedScope.ServiceProvider
        .GetRequiredService<TodoAppDbContext>();
    logger.LogInformation(
        "Seeding demo workspace with owner {Email}.",
        DevelopmentDataSeeder.DemoOwnerEmail);
    await DevelopmentDataSeeder.SeedAsync(
        database,
        CancellationToken.None);
}

app.MapStaticAssets();
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
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

static bool ShouldApplyMigrations(
    IWebHostEnvironment environment,
    IConfiguration configuration) =>
    environment.IsDevelopment() ||
    bool.TryParse(
        configuration["Database:ApplyMigrationsOnStartup"],
        out var applyMigrations) && applyMigrations;

static bool ShouldSeedDemoData(
    IWebHostEnvironment environment,
    IConfiguration configuration) =>
    environment.IsDevelopment() ||
    bool.TryParse(
        configuration["DemoData:SeedOnStartup"],
        out var seedDemoData) && seedDemoData;

static void ValidateDeploymentConfiguration(
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
    {
        return;
    }

    Require(
        configuration.GetConnectionString("TodoApp"),
        "ConnectionStrings:TodoApp");
    Require(
        configuration["Authentication:Authority"],
        "Authentication:Authority");
    Require(
        configuration["Authentication:Audience"],
        "Authentication:Audience");

    var origins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    if (origins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must contain the deployed frontend origin.");
    }

    if (bool.TryParse(
            configuration["Email:Smtp:Enabled"],
            out var smtpEnabled) &&
        smtpEnabled)
    {
        Require(
            configuration["Email:Smtp:Host"],
            "Email:Smtp:Host");
        Require(
            configuration["Email:Smtp:FromAddress"],
            "Email:Smtp:FromAddress");
    }

    static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is required outside Development and Testing.");
        }
    }
}

public partial class Program;
