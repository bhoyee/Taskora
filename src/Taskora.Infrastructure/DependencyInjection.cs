using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application.Abstractions;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Repositories;
using TodoApp.Infrastructure.Services;

namespace TodoApp.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: registers the EF Core
/// <see cref="TodoAppDbContext"/> (switching between SQL Server, Postgres,
/// and SQLite based on configuration), all repository implementations, and
/// the small cross-cutting services (email, clock, business date, id
/// generation, link building) that the Application layer depends on via
/// abstractions.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure-layer services into <paramref name="services"/>.
    /// The database provider is chosen from the "Database:Provider"
    /// configuration key (defaulting to SQLite for local development), with
    /// its connection string normalized to handle both classic and URL-style
    /// Postgres formats.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider =
            configuration["Database:Provider"] ?? "Sqlite";
        var connectionString =
            ConnectionStringNormalizer.ForProvider(
                provider,
                configuration.GetConnectionString("TodoApp") ??
                    throw new InvalidOperationException(
                        "Connection string 'TodoApp' is required."));

        services.AddDbContext<TodoAppDbContext>(options =>
        {
            ConfigureMigrationWarnings(options);

            // Provider selection: SQL Server and Postgres both enable
            // connection retry-on-failure for resilience against transient
            // cloud database blips; SQLite (local/dev) needs no such retry.
            if (provider.Equals(
                    "SqlServer",
                    StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    connectionString,
                    sql => sql.EnableRetryOnFailure());
            }
            else if (ConnectionStringNormalizer.IsPostgres(provider))
            {
                options.UseNpgsql(
                    connectionString,
                    postgres => postgres.EnableRetryOnFailure());
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        // Repository registrations. Several read-only/read-model interfaces
        // are resolved to the same concrete repository instance (registered
        // once as itself, then exposed under each interface) so a single
        // scoped instance serves both write and read-side abstractions.
        services.AddScoped<ProjectRepository>();
        services.AddScoped<IProjectRepository>(
            provider => provider.GetRequiredService<ProjectRepository>());
        services.AddScoped<TaskRepository>();
        services.AddScoped<ITaskRepository>(
            provider => provider.GetRequiredService<TaskRepository>());
        services.AddScoped<ITaskReadRepository>(
            provider => provider.GetRequiredService<TaskRepository>());
        services.AddScoped<IPersonalTodoRepository,
            PersonalTodoRepository>();
        services.AddScoped<IDailyRoutineRepository,
            DailyRoutineRepository>();
        services.AddScoped<IProjectBoardReadRepository,
            ProjectBoardReadRepository>();
        services.AddScoped<ITaskActivityReadRepository,
            TaskActivityReadRepository>();
        services.AddScoped<IPortfolioDashboardReadRepository,
            PortfolioDashboardReadRepository>();
        services.AddScoped<IWorkspaceReportReadRepository,
            WorkspaceReportReadRepository>();
        services.AddScoped<IPlatformReadRepository,
            PlatformReadRepository>();
        services.AddScoped<IDueDateNotificationReadRepository,
            DueDateNotificationReadRepository>();

        // Email options are read up front (outside the AddDbContext-style
        // deferred configuration) so the same values can both populate
        // SmtpEmailOptions via Configure(...) and decide, below, which
        // INotificationEmailSender implementation to register.
        var smtpOptions = ReadSmtpOptions(configuration);
        services.Configure<SmtpEmailOptions>(options =>
        {
            options.Enabled = smtpOptions.Enabled;
            options.Host = smtpOptions.Host;
            options.Port = smtpOptions.Port;
            options.UseSsl = smtpOptions.UseSsl;
            options.Username = smtpOptions.Username;
            options.Password = smtpOptions.Password;
            options.FromAddress = smtpOptions.FromAddress;
            options.FromName = smtpOptions.FromName;
        });
        var applicationUrlOptions = ReadApplicationUrlOptions(configuration);
        services.Configure<ApplicationUrlOptions>(options =>
        {
            options.PublicBaseUrl = applicationUrlOptions.PublicBaseUrl;
        });
        services.AddSingleton<IApplicationLinkBuilder, ApplicationLinkBuilder>();
        // Swap the notification email implementation based on configuration:
        // real SMTP delivery when enabled, otherwise a logging stub so
        // notification flows still work (without sending mail) locally.
        if (smtpOptions.Enabled)
        {
            services.AddScoped<INotificationEmailSender,
                SmtpNotificationEmailSender>();
        }
        else
        {
            services.AddScoped<INotificationEmailSender,
                LoggingNotificationEmailSender>();
        }

        // Singleton: only needs IServiceScopeFactory (itself a singleton) to
        // create its own scope per dispatch, so it doesn't need to be scoped
        // to any one request.
        services.AddSingleton<IBackgroundEmailDispatcher,
            BackgroundEmailDispatcher>();

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IWorkspaceInvitationRepository,
            WorkspaceInvitationRepository>();
        services.AddScoped<IUnitOfWork>(
            provider => provider.GetRequiredService<TodoAppDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<BusinessDateOptions>(options =>
        {
            options.TimeZoneId = string.IsNullOrWhiteSpace(
                configuration["App:TimeZoneId"])
                ? "Europe/London"
                : configuration["App:TimeZoneId"]!;
        });
        services.AddSingleton<IBusinessDateProvider, BusinessDateProvider>();
        services.AddSingleton<IIdentifierGenerator,
            GuidIdentifierGenerator>();

        return services;
    }

    // Suppresses EF Core's "pending model changes" warning, which can be a
    // false positive here since one migration history is shared across two
    // providers (SQLite and Postgres) with differing type metadata.
    private static void ConfigureMigrationWarnings(
        DbContextOptionsBuilder options)
    {
        // The portfolio app supports SQLite locally and Postgres in production
        // with one migration history. Provider-specific type metadata can make
        // EF think the model has pending changes during deployment migrations.
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    // Reads SMTP settings from configuration into a plain options object,
    // applying the same defaults as the SmtpEmailOptions property initializers.
    private static SmtpEmailOptions ReadSmtpOptions(
        IConfiguration configuration) =>
        new()
        {
            Enabled = ReadBool(configuration["Email:Smtp:Enabled"]),
            Host = configuration["Email:Smtp:Host"] ?? string.Empty,
            Port = ReadInt(configuration["Email:Smtp:Port"], 587),
            UseSsl = ReadBool(configuration["Email:Smtp:UseSsl"], true),
            Username = configuration["Email:Smtp:Username"] ?? string.Empty,
            Password = configuration["Email:Smtp:Password"] ?? string.Empty,
            FromAddress = configuration["Email:Smtp:FromAddress"] ?? string.Empty,
            FromName = string.IsNullOrWhiteSpace(
                configuration["Email:Smtp:FromName"])
                ? "Taskora"
                : configuration["Email:Smtp:FromName"]!
        };

    // Reads the public base URL from configuration, falling back to the
    // local dev default when unset or blank.
    private static ApplicationUrlOptions ReadApplicationUrlOptions(
        IConfiguration configuration) =>
        new()
        {
            PublicBaseUrl = string.IsNullOrWhiteSpace(
                configuration["App:PublicBaseUrl"])
                ? "http://localhost:5173"
                : configuration["App:PublicBaseUrl"]!
        };

    // Simple configuration-value parsing helpers with a fallback default.
    private static bool ReadBool(string? value, bool defaultValue = false) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    private static int ReadInt(string? value, int defaultValue) =>
        int.TryParse(value, out var result) ? result : defaultValue;

}
