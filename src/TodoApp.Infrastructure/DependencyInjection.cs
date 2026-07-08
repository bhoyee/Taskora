using Microsoft.EntityFrameworkCore;
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
            configuration.GetConnectionString("TodoApp") ??
            throw new InvalidOperationException(
                "Connection string 'TodoApp' is required.");

        services.AddDbContext<TodoAppDbContext>(options =>
        {
            if (provider.Equals(
                    "SqlServer",
                    StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    connectionString,
                    sql => sql.EnableRetryOnFailure());
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
        services.AddScoped<IProjectBoardReadRepository,
            ProjectBoardReadRepository>();
        services.AddScoped<ITaskActivityReadRepository,
            TaskActivityReadRepository>();
        services.AddScoped<IPortfolioDashboardReadRepository,
            PortfolioDashboardReadRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IUnitOfWork>(
            provider => provider.GetRequiredService<TodoAppDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdentifierGenerator,
            GuidIdentifierGenerator>();

        return services;
    }
}
