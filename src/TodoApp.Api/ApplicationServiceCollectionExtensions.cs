using TodoApp.Application.Projects;
using TodoApp.Application.Accounts;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Intelligence;
using TodoApp.Application.Projects.Board;
using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Application.Tasks.Activity;
using TodoApp.Application.Tasks.Assignment;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Application.Tasks.Metadata;
using TodoApp.Application.Tasks.Queries;

namespace TodoApp.Api;

internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(
        this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<RegisterAccountHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<CreateWorkspaceHandler>();
        services.AddScoped<GetMyWorkspacesHandler>();
        services.AddScoped<GetWorkspaceMembersHandler>();
        services.AddScoped<AddWorkspaceMemberHandler>();
        services.AddScoped<ChangeWorkspaceRoleHandler>();
        services.AddScoped<RemoveWorkspaceMemberHandler>();
        services.AddScoped<GetPortfolioDashboardHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();
        services.AddScoped<GetProjectByIdHandler>();
        services.AddScoped<ListWorkspaceProjectsHandler>();
        services.AddScoped<CreateWorkspaceProjectHandler>();
        services.AddScoped<GetProjectBoardHandler>();

        services.AddScoped<CreateTaskHandler>();
        services.AddScoped<GetTaskActivityHandler>();
        services.AddScoped<AssignTaskHandler>();
        services.AddScoped<UnassignTaskHandler>();
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
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateTaskCategoryHandler>();
        services.AddScoped<AddTaskTagHandler>();
        services.AddScoped<RemoveTaskTagHandler>();
        services.AddScoped<AddTaskNoteHandler>();

        return services;
    }
}
