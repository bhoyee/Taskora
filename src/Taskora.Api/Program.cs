using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TodoApp.Api;
using TodoApp.Api.Diagnostics;
using TodoApp.Api.Endpoints;
using TodoApp.Api.Notifications;
using TodoApp.Api.Operations;
using TodoApp.Api.Realtime;
using TodoApp.Api.Security;
using TodoApp.Infrastructure;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

// Application composition root: wires up configuration, services, the
// middleware pipeline, and endpoint mapping for the whole API host.

// Load local developer secrets/config from a .env file (if present) into
// process environment variables before the host reads configuration.
LoadEnvironmentFile();

// Some container hosts (e.g. Render) cap inotify instances low enough that
// ASP.NET Core's default appsettings.json file-watcher (reloadOnChange: true)
// throws IOException during WebApplication.CreateBuilder and crash-loops the
// whole process. This must be set before CreateBuilder runs, since it reads
// this setting from the environment while building host configuration.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

var builder = WebApplication.CreateBuilder(args);

// --- Diagnostics/logging setup -------------------------------------------
// Keep a short in-memory log window for the super-admin Operations UI, while
// also writing durable JSONL files for local/server troubleshooting.
var operationLogs = new InMemoryLogStore(
    ReadPositiveInt(builder.Configuration["Operations:Logs:MaxEntries"], 200),
    ReadPositiveInt(builder.Configuration["Operations:Logs:RetentionDays"], 30));
builder.Services.AddSingleton(operationLogs);
builder.Logging.AddProvider(new InMemoryLoggerProvider(operationLogs));

// Optional durable JSONL file logging, configurable via Operations:Logs:*.
var fileLogOptions = new FileLoggerOptions
{
    Enabled = ReadBool(
        builder.Configuration["Operations:Logs:FileEnabled"],
        true),
    Directory = string.IsNullOrWhiteSpace(
        builder.Configuration["Operations:Logs:Directory"])
        ? "App_Data/logs"
        : builder.Configuration["Operations:Logs:Directory"]!,
    RetentionDays = ReadPositiveInt(
        builder.Configuration["Operations:Logs:RetentionDays"],
        30)
};
if (fileLogOptions.Enabled)
{
    builder.Logging.AddProvider(new FileLoggerProvider(fileLogOptions));
}

// Status/state singletons shared by background hosted services and the
// Operations UI so callers can observe scheduler/backup progress.
builder.Services.AddSingleton<DueDateReminderSchedulerStatus>();
builder.Services.AddSingleton<DatabaseBackupSchedulerStatus>();
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddSingleton<WorkspaceEventBroadcaster>();

// Fail fast outside Development/Testing if required deployment settings
// (connection string, auth authority/audience, CORS origins, SMTP) are
// missing, rather than starting up in a broken state.
ValidateDeploymentConfiguration(
    builder.Environment,
    builder.Configuration);

// --- Core ASP.NET Core services -------------------------------------------
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Restrict cross-origin requests to the configured frontend origin(s) only.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ConfiguredFrontend",
        policy =>
        {
            var origins = GetAllowedOrigins(
                builder.Configuration,
                builder.Environment);

            if (origins.Length > 0)
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
});
// Serialize enums as their string names in JSON payloads instead of ints.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()));

// Health checks: "live" reports whether the process can answer traffic at
// all; "ready" checks (CORS, email, auth, DB, background reminder runner)
// report whether the app is fully configured/operational, surfaced at
// /health, /health/ready and /health/live below.
builder.Services.AddHealthChecks()
    .AddCheck(
        "API running",
        () => HealthCheckResult.Healthy(
            "HTTP API is running and can answer live traffic."),
        tags: ["live"])
    .AddCheck(
        "CORS configuration",
        () =>
        {
            var origins = GetAllowedOrigins(
                builder.Configuration,
                builder.Environment);

            return origins.Length == 0
                ? HealthCheckResult.Degraded(
                    "No frontend origins are configured. Set Cors:AllowedOrigins or App:PublicBaseUrl for production.")
                : HealthCheckResult.Healthy(
                    $"Allowed frontend origins: {string.Join(", ", origins)}.");
        },
        tags: ["ready"])
    .AddCheck(
        "Email notifications",
        () =>
        {
            var enabled = ReadBool(
                builder.Configuration["Email:Smtp:Enabled"]);
            if (!enabled)
            {
                return HealthCheckResult.Degraded(
                    "SMTP is disabled. Emails are written to application logs only.");
            }

            var host = builder.Configuration["Email:Smtp:Host"];
            var from = builder.Configuration["Email:Smtp:FromAddress"];
            return string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(from)
                    ? HealthCheckResult.Unhealthy(
                        "SMTP is enabled but host/from address is missing.")
                    : HealthCheckResult.Healthy(
                        $"SMTP is configured for {host}.");
        },
        tags: ["ready"])
    .AddCheck(
        "Authentication configuration",
        () =>
        {
            if (builder.Environment.IsDevelopment())
            {
                return HealthCheckResult.Degraded(
                    "Development header authentication is enabled.");
            }

            if (SecurityServiceCollectionExtensions
                .UsesAppTokenAuthentication(builder.Configuration))
            {
                return HealthCheckResult.Degraded(
                    "Application account token authentication is enabled. Configure a JWT authority for external identity-provider validation.");
            }

            var authority = builder.Configuration["Authentication:Authority"];
            var audience = builder.Configuration["Authentication:Audience"];
            return string.IsNullOrWhiteSpace(authority) ||
                string.IsNullOrWhiteSpace(audience)
                ? HealthCheckResult.Unhealthy(
                    "Authentication authority or audience is missing.")
                : HealthCheckResult.Healthy(
                    "Authentication authority and audience are configured.");
        },
        tags: ["ready"])
    .AddCheck<DueDateReminderHealthCheck>(
        "Due date reminder runner",
        tags: ["ready"])
    .AddDbContextCheck<TodoAppDbContext>(
        "Database",
        tags: ["ready"]);

// Application/infrastructure/security module registrations (each module
// wires up its own use-case handlers, repositories, and auth scheme).
builder.Services.AddApplicationUseCases();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTodoSecurity(
    builder.Environment,
    builder.Configuration);

// Background hosted services: due-date reminder emails and scheduled DB backups.
builder.Services.AddHostedService<DueDateReminderScheduler>();
builder.Services.AddHostedService<DatabaseBackupScheduler>();

var app = builder.Build();

// --- HTTP request pipeline (order matters) --------------------------------
// Correlation IDs are added before exception handling so failed requests can
// still be matched between the browser response, Operations page, and log file.
app.UseMiddleware<CorrelationIdMiddleware>();
// Routes unhandled exceptions to ApiExceptionHandler for a ProblemDetails response.
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("ConfiguredFrontend");
// Short-circuit the liveness probe before authentication/authorization so
// container orchestrators can check process health without credentials.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals(
            "/health/live",
            StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync("Healthy");
        return;
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();

// Expose the OpenAPI document only in Development (not for production/public access).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- Database schema/startup bootstrap ------------------------------------
// EnsureCreated is a shortcut for initial portfolio/demo bootstrap only;
// real deployments should rely on migrations instead.
if (ShouldEnsureCreated(app.Configuration))
{
    await using var schemaScope = app.Services.CreateAsyncScope();
    var logger = schemaScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var database = schemaScope.ServiceProvider
        .GetRequiredService<TodoAppDbContext>();
    logger.LogWarning(
        "Ensuring the database schema exists. Use this only for initial portfolio database bootstrap.");
    await database.Database.EnsureCreatedAsync();
}
// Apply pending EF Core migrations automatically (Development always, or
// when explicitly enabled via Database:ApplyMigrationsOnStartup).
else if (ShouldApplyMigrations(app.Environment, app.Configuration))
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

// Seed a demo workspace/owner (Development always, or when explicitly
// enabled via DemoData:SeedOnStartup) so the app has usable sample data.
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

// --- Endpoint mapping ------------------------------------------------------
app.MapStaticAssets();
// Both /health and /health/ready expose the same "ready" checks; /health/live
// is handled earlier by the short-circuit middleware above.
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
// Register each feature module's minimal-API route groups.
app.MapProjectEndpoints();
app.MapTaskEndpoints();
app.MapPersonalTodoEndpoints();
app.MapIntelligenceEndpoints();
app.MapNotificationEndpoints();
app.MapWorkspaceEndpoints();
app.MapAccountEndpoints();
app.MapOperationsEndpoints();
app.MapPlatformEndpoints();
app.MapRealtimeEndpoints();
// Catch-all for any /api/* route that didn't match a mapped endpoint above,
// so unknown API calls get a JSON 404 instead of falling through to the SPA fallback.
app.Map("/api/{**path}", () => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "API endpoint not found."));

// Resolve the SPA's index.html: the published wwwroot copy when deployed,
// or the Vite dev build output when running the API against a local frontend build.
var publishedIndex = Path.Combine(
    app.Environment.ContentRootPath,
    "wwwroot",
    "index.html");
var developmentIndex = Path.GetFullPath(
    Path.Combine(
        app.Environment.ContentRootPath,
        "..",
        "Taskora.Web",
        "dist",
        "index.html"));
var webIndex = File.Exists(publishedIndex)
    ? publishedIndex
    : developmentIndex;
// Serve the SPA shell for any non-API route so client-side routing works on refresh/deep links.
app.MapFallback(() => Results.File(webIndex, "text/html"));

app.Run();

// --- Local helper functions (startup configuration logic) -----------------

// Walks up from the current directory looking for a .env file and copies any
// KEY=VALUE lines into process environment variables (without overwriting
// variables already set), for local development convenience.
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

// True in Development, or when Database:ApplyMigrationsOnStartup is set.
static bool ShouldApplyMigrations(
    IWebHostEnvironment environment,
    IConfiguration configuration) =>
    environment.IsDevelopment() ||
    bool.TryParse(
        configuration["Database:ApplyMigrationsOnStartup"],
        out var applyMigrations) && applyMigrations;

// True only when Database:EnsureCreatedOnStartup is explicitly enabled.
static bool ShouldEnsureCreated(IConfiguration configuration) =>
    bool.TryParse(
        configuration["Database:EnsureCreatedOnStartup"],
        out var ensureCreated) && ensureCreated;

// Builds the set of allowed CORS origins from configuration (plus the public
// base URL, plus a local dev default), normalizing and de-duplicating them.
static string[] GetAllowedOrigins(
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var origins = new List<string>();
    var configuredOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    origins.AddRange(configuredOrigins);

    var publicBaseUrl = configuration["App:PublicBaseUrl"];
    if (!string.IsNullOrWhiteSpace(publicBaseUrl))
    {
        origins.Add(publicBaseUrl);
    }

    if (origins.Count == 0 && environment.IsDevelopment())
    {
        origins.Add("http://localhost:5173");
    }

    return origins
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Where(origin => Uri.TryCreate(
            origin,
            UriKind.Absolute,
            out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

// True in Development, or when DemoData:SeedOnStartup is set.
static bool ShouldSeedDemoData(
    IWebHostEnvironment environment,
    IConfiguration configuration) =>
    environment.IsDevelopment() ||
    bool.TryParse(
        configuration["DemoData:SeedOnStartup"],
        out var seedDemoData) && seedDemoData;

// Parses a config string as bool, falling back to defaultValue when missing/invalid.
static bool ReadBool(string? value, bool defaultValue = false) =>
    bool.TryParse(value, out var result) ? result : defaultValue;

// Parses a config string as a positive int, falling back to defaultValue otherwise.
static int ReadPositiveInt(string? value, int defaultValue) =>
    int.TryParse(value, out var result) && result > 0
        ? result
        : defaultValue;

// Throws InvalidOperationException outside Development/Testing if required
// deployment settings (DB connection string, auth authority/audience unless
// app-token auth is used, CORS origins, SMTP host/from when SMTP is enabled)
// are missing, so misconfiguration is caught at startup rather than at runtime.
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
    if (!SecurityServiceCollectionExtensions
        .UsesAppTokenAuthentication(configuration))
    {
        Require(
            configuration["Authentication:Authority"],
            "Authentication:Authority");
        Require(
            configuration["Authentication:Audience"],
            "Authentication:Audience");
    }

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

    // Throws if the given configuration value is missing/blank.
    static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is required outside Development and Testing.");
        }
    }
}

// Partial class declaration exposing the top-level program as a named type,
// so WebApplicationFactory<Program> can be used to host it in integration tests.
public partial class Program;
