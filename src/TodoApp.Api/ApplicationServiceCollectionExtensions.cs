using TodoApp.Application.Projects;
using TodoApp.Application.Projects.Board;
using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Application.Tasks.Queries;

namespace TodoApp.Api;

internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(
        this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();
        services.AddScoped<GetProjectByIdHandler>();
        services.AddScoped<GetProjectBoardHandler>();

        services.AddScoped<CreateTaskHandler>();
        services.AddScoped<GetTaskByIdHandler>();
        services.AddScoped<SearchTasksHandler>();
        services.AddScoped<MoveTaskToReadyHandler>();
        services.AddScoped<StartTaskHandler>();
        services.AddScoped<CompleteTaskHandler>();
        services.AddScoped<UpdateTaskHandler>();
        services.AddScoped<BlockTaskHandler>();
        services.AddScoped<UnblockTaskHandler>();
        services.AddScoped<ReopenTaskHandler>();
        services.AddScoped<UpdatePlanningFactorsHandler>();
        services.AddScoped<AddTaskDependencyHandler>();
        services.AddScoped<RemoveTaskDependencyHandler>();

        return services;
    }
}
