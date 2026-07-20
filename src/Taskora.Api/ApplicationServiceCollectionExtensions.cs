using TodoApp.Application.Projects;
using TodoApp.Application.Accounts;
using TodoApp.Application.Collaboration;
using TodoApp.Application.Intelligence;
using TodoApp.Application.Notifications;
using TodoApp.Application.Projects.Board;
using TodoApp.Application.Tasks.CreateTask;
using TodoApp.Application.Tasks.Activity;
using TodoApp.Application.Tasks.Assignment;
using TodoApp.Application.Tasks.Lifecycle;
using TodoApp.Application.Tasks.Maintenance;
using TodoApp.Application.Tasks.Metadata;
using TodoApp.Application.Tasks.Queries;
using TodoApp.Application.Todos;

namespace TodoApp.Api;

internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(
        this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<RegisterAccountHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetCurrentAccountHandler>();
        services.AddScoped<UpdateAccountProfileHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<RequestPasswordResetHandler>();
        services.AddScoped<ResetPasswordWithTokenHandler>();
        services.AddScoped<CreateWorkspaceHandler>();
        services.AddScoped<UpdateWorkspaceHandler>();
        services.AddScoped<DeleteWorkspaceHandler>();
        services.AddScoped<GetMyWorkspacesHandler>();
        services.AddScoped<GetWorkspaceMembersHandler>();
        services.AddScoped<AddWorkspaceMemberHandler>();
        services.AddScoped<ChangeWorkspaceRoleHandler>();
        services.AddScoped<RemoveWorkspaceMemberHandler>();
        services.AddScoped<InviteWorkspaceMemberHandler>();
        services.AddScoped<GetWorkspaceInvitationsHandler>();
        services.AddScoped<GetWorkspaceInvitationByTokenHandler>();
        services.AddScoped<AcceptWorkspaceInvitationHandler>();
        services.AddScoped<DeclineWorkspaceInvitationHandler>();
        services.AddScoped<CancelWorkspaceInvitationHandler>();
        services.AddScoped<GetPortfolioDashboardHandler>();
        services.AddScoped<GetWorkspaceReportHandler>();
        services.AddScoped<SendDueDateNotificationsHandler>();
        services.AddScoped<SendPersonalTodoCarryOverNotificationsHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();
        services.AddScoped<DeleteProjectHandler>();
        services.AddScoped<GetProjectByIdHandler>();
        services.AddScoped<ListWorkspaceProjectsHandler>();
        services.AddScoped<CreateWorkspaceProjectHandler>();
        services.AddScoped<GetProjectBoardHandler>();
        services.AddScoped<CreateSprintHandler>();
        services.AddScoped<UpdateSprintHandler>();
        services.AddScoped<StartSprintHandler>();
        services.AddScoped<CompleteSprintHandler>();
        services.AddScoped<CancelSprintHandler>();

        services.AddScoped<CreateTaskHandler>();
        services.AddScoped<GetTaskActivityHandler>();
        services.AddScoped<GetWorkspaceActivityHandler>();
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
        services.AddScoped<ResumeTaskHandler>();
        services.AddScoped<ReopenTaskHandler>();
        services.AddScoped<DeleteTaskHandler>();
        services.AddScoped<UpdatePlanningFactorsHandler>();
        services.AddScoped<AddTaskDependencyHandler>();
        services.AddScoped<RemoveTaskDependencyHandler>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateTaskCategoryHandler>();
        services.AddScoped<AddTaskTagHandler>();
        services.AddScoped<RemoveTaskTagHandler>();
        services.AddScoped<AddTaskNoteHandler>();

        services.AddScoped<ListPersonalTodosHandler>();
        services.AddScoped<CreatePersonalTodoHandler>();
        services.AddScoped<UpdatePersonalTodoHandler>();
        services.AddScoped<CompletePersonalTodoHandler>();
        services.AddScoped<ReopenPersonalTodoHandler>();
        services.AddScoped<DeletePersonalTodoHandler>();
        services.AddScoped<ListDailyRoutinesHandler>();
        services.AddScoped<CreateDailyRoutineHandler>();
        services.AddScoped<UpdateDailyRoutineHandler>();
        services.AddScoped<DeleteDailyRoutineHandler>();
        services.AddScoped<GenerateDailyRoutineTodosHandler>();

        return services;
    }
}
