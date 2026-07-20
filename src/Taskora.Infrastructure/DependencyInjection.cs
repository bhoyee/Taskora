using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application.Abstractions;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Repositories;
using TodoApp.Infrastructure.Services;

namespace TodoApp.Infrastructure;

public static class DependencyInjection
{
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
        services.AddScoped<IDueDateNotificationReadRepository,
            DueDateNotificationReadRepository>();
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

    private static void ConfigureMigrationWarnings(
        DbContextOptionsBuilder options)
    {
        // The portfolio app supports SQLite locally and Postgres in production
        // with one migration history. Provider-specific type metadata can make
        // EF think the model has pending changes during deployment migrations.
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

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

    private static ApplicationUrlOptions ReadApplicationUrlOptions(
        IConfiguration configuration) =>
        new()
        {
            PublicBaseUrl = string.IsNullOrWhiteSpace(
                configuration["App:PublicBaseUrl"])
                ? "http://localhost:5173"
                : configuration["App:PublicBaseUrl"]!
        };

    private static bool ReadBool(string? value, bool defaultValue = false) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    private static int ReadInt(string? value, int defaultValue) =>
        int.TryParse(value, out var result) ? result : defaultValue;

}
