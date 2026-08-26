using Microsoft.EntityFrameworkCore;
using TodoApp.Application.Projects;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Seeding;

/// <summary>
/// Populates a workspace and four role personas (Super Admin/Owner/Manager/
/// Member) for the public "View demo" landing-page login, entirely separate
/// from <see cref="DevelopmentDataSeeder"/>'s data. Every id and email here
/// uses its own dedicated namespace (the "c" GUID prefix, "demo-" email
/// prefix) that is guaranteed never to collide with a real account or
/// workspace, including the ones DevelopmentDataSeeder creates — this class
/// must never read or write any id defined there. Idempotent like
/// DevelopmentDataSeeder: each stage checks existence before inserting.
/// </summary>
public static class PublicDemoSeeder
{
    public static readonly Guid OwnerId =
        Guid.Parse("c1000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId =
        Guid.Parse("c1000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId =
        Guid.Parse("c1000000-0000-0000-0000-000000000003");
    public static readonly Guid SuperAdminId =
        Guid.Parse("c1000000-0000-0000-0000-000000000004");
    public static readonly Guid WorkspaceId =
        Guid.Parse("c2000000-0000-0000-0000-000000000001");
    public const string OwnerEmail = "demo-owner@example.com";
    public const string ManagerEmail = "demo-manager@example.com";
    public const string MemberEmail = "demo-member@example.com";
    public const string SuperAdminEmail = "demo-superadmin@example.com";
    public const string Password = "TaskoraDemo123!";

    private static readonly Guid ProjectId =
        Guid.Parse("c3000000-0000-0000-0000-000000000001");
    private static readonly Guid SprintProjectId =
        Guid.Parse("c3000000-0000-0000-0000-000000000002");
    private static readonly Guid ClosedProjectId =
        Guid.Parse("c3000000-0000-0000-0000-000000000003");
    private static readonly Guid OperationsCategoryId =
        Guid.Parse("c4000000-0000-0000-0000-000000000001");
    private static readonly Guid ReleaseCategoryId =
        Guid.Parse("c4000000-0000-0000-0000-000000000002");
    private static readonly Guid ActiveSprintId =
        Guid.Parse("c5000000-0000-0000-0000-000000000001");
    private static readonly Guid PlannedSprintId =
        Guid.Parse("c5000000-0000-0000-0000-000000000002");
    private static readonly Guid OnboardingSprintId =
        Guid.Parse("c5000000-0000-0000-0000-000000000003");
    private static readonly Guid OwnerTodoId =
        Guid.Parse("c6000000-0000-0000-0000-000000000001");
    private static readonly Guid ManagerTodoId =
        Guid.Parse("c6000000-0000-0000-0000-000000000002");
    private static readonly Guid MemberTodoId =
        Guid.Parse("c6000000-0000-0000-0000-000000000003");
    private static readonly Guid MemberTodoCommentId =
        Guid.Parse("c7000000-0000-0000-0000-000000000001");
    private static readonly Guid DailyRoutineId =
        Guid.Parse("c8000000-0000-0000-0000-000000000001");
    private static readonly Guid PendingInvitationId =
        Guid.Parse("c9000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Seeds the demo personas/workspace and demo projects/tasks/todos/
    /// routine/invitation if they don't already exist. Never touches any id
    /// outside this class's own namespace.
    /// </summary>
    public static async Task SeedAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        await EnsureUserAsync(context, OwnerId, "Demo Owner", OwnerEmail, cancellationToken);
        await EnsureUserAsync(context, ManagerId, "Demo Manager", ManagerEmail, cancellationToken);
        await EnsureUserAsync(context, MemberId, "Demo Member", MemberEmail, cancellationToken);
        await EnsureUserAsync(context, SuperAdminId, "Demo Super Admin", SuperAdminEmail, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces
            .Include("_memberships")
            .SingleOrDefaultAsync(w => w.Id == WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            workspace = Workspace.Create(WorkspaceId, "Demo workspace", OwnerId);
            context.Workspaces.Add(workspace);
        }
        EnsureMember(workspace, ManagerId, WorkspaceRole.Manager);
        EnsureMember(workspace, MemberId, WorkspaceRole.Member);
        // Super Admin is a plain Member here too — their extra access to
        // Platform/Operations/Backups comes purely from their email being on
        // the SuperAdminEmails allowlist, not from anything workspace-specific.
        EnsureMember(workspace, SuperAdminId, WorkspaceRole.Member);
        await context.SaveChangesAsync(cancellationToken);

        await AddMissingCredentialAsync(context, OwnerId, cancellationToken);
        await AddMissingCredentialAsync(context, ManagerId, cancellationToken);
        await AddMissingCredentialAsync(context, MemberId, cancellationToken);
        await AddMissingCredentialAsync(context, SuperAdminId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (!await context.Projects.AnyAsync(p => p.Id == ProjectId, cancellationToken))
        {
            await SeedProjectsAndTasksAsync(context, cancellationToken);
        }

        await SeedPersonalTodosAsync(context, cancellationToken);
        await SeedDailyRoutineAsync(context, cancellationToken);
        await SeedPendingInvitationAsync(context, cancellationToken);
    }

    /// <summary>
    /// Deletes the mutable "content" tier of the demo data (projects/sprints/
    /// tasks, My Day todos, the daily routine, and the pending invitation) for
    /// this class's own fixed ids, then re-seeds it via <see cref="SeedAsync"/>
    /// — restoring a pristine demo without touching the identity tier (the
    /// four accounts, their credentials, the workspace, or memberships), so
    /// existing demo logins keep working across a reset. Project deletion goes
    /// through the same <see cref="DeleteProjectHandler"/>/<c>HasAdministrativeBypass</c>
    /// path the Platform page's hard-delete already uses.
    /// </summary>
    public static async Task ResetContentAsync(
        TodoAppDbContext context,
        DeleteProjectHandler deleteProjectHandler,
        CancellationToken cancellationToken)
    {
        foreach (var projectId in new[] { ProjectId, SprintProjectId, ClosedProjectId })
        {
            await deleteProjectHandler.HandleAsync(
                new DeleteProjectCommand(projectId, HasAdministrativeBypass: true),
                cancellationToken);
        }

        var todos = await context.PersonalTodos
            .Where(todo => todo.Id == OwnerTodoId
                || todo.Id == ManagerTodoId
                || todo.Id == MemberTodoId)
            .ToListAsync(cancellationToken);
        context.PersonalTodos.RemoveRange(todos);

        var routine = await context.DailyRoutines
            .SingleOrDefaultAsync(r => r.Id == DailyRoutineId, cancellationToken);
        if (routine is not null)
        {
            context.DailyRoutines.Remove(routine);
        }

        var invitation = await context.WorkspaceInvitations
            .SingleOrDefaultAsync(i => i.Id == PendingInvitationId, cancellationToken);
        if (invitation is not null)
        {
            context.WorkspaceInvitations.Remove(invitation);
        }

        await context.SaveChangesAsync(cancellationToken);

        await SeedAsync(context, cancellationToken);
    }

    private static async Task SeedProjectsAndTasksAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            "Sample project for the public Taskora demo.",
            WorkspaceId);
        project.SetTargetDate(
            DueDate.Create(DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(30))));
        project.AddCategory(OperationsCategoryId, "Operations");
        project.AddCategory(ReleaseCategoryId, "Release");
        var activeSprint = project.AddSprint(
            ActiveSprintId,
            "Portfolio hardening sprint",
            "Stabilise collaboration, sprint planning, reminders, and dashboard readiness.",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)));
        activeSprint.Start();
        project.AddSprint(
            PlannedSprintId,
            "Deployment polish sprint",
            "Prepare the final deployment story, production settings, and demo walkthrough.",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)));

        var backlog = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000001"),
            ProjectId,
            "Review portfolio requirements");
        backlog.RecordCreator(OwnerId);
        backlog.Assign(MemberId);
        backlog.AssignCategory(OperationsCategoryId);
        backlog.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(14))));
        backlog.Estimate(EffortEstimate.Create(3));
        backlog.SetPlanningFactors(PlanningFactors.Create(3, 2, 2, 3));
        backlog.AssignSprint(PlannedSprintId);
        backlog.AddTag("planning");
        backlog.AddNote(
            Guid.Parse("c3200000-0000-0000-0000-000000000001"),
            OwnerId,
            "Confirm acceptance criteria before delivery planning.",
            DateTimeOffset.UtcNow.AddDays(-4));

        var ready = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000002"),
            ProjectId,
            "Prepare deployment checklist");
        ready.RecordCreator(ManagerId);
        ready.Assign(ManagerId);
        ready.AssignCategory(OperationsCategoryId);
        ready.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(2))));
        ready.Estimate(EffortEstimate.Create(2));
        ready.SetPlanningFactors(
            PlanningFactors.Create(4, 4, 3, 3));
        ready.AssignSprint(ActiveSprintId);
        ready.MoveToReady();
        ready.AddTag("deployment");
        ready.AddNote(
            Guid.Parse("c3200000-0000-0000-0000-000000000002"),
            ManagerId,
            "Checklist is ready for final review.",
            DateTimeOffset.UtcNow.AddDays(-2));

        var blocked = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000003"),
            ProjectId,
            "Publish production release");
        blocked.RecordCreator(OwnerId);
        blocked.Assign(ManagerId);
        blocked.AssignCategory(ReleaseCategoryId);
        blocked.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(1))));
        blocked.Estimate(EffortEstimate.Create(5));
        blocked.SetPlanningFactors(
            PlanningFactors.Create(5, 5, 4, 3));
        blocked.AssignSprint(ActiveSprintId);
        blocked.MoveToReady();
        blocked.Start();
        blocked.Block("Waiting for deployment approval");
        blocked.AddTag("release");
        blocked.AddTag("blocked");
        blocked.AddNote(
            Guid.Parse("c3200000-0000-0000-0000-000000000003"),
            OwnerId,
            "Approval is the current release risk.",
            DateTimeOffset.UtcNow.AddDays(-1));

        var inProgress = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000004"),
            ProjectId,
            "Validate dashboard analytics");
        inProgress.RecordCreator(ManagerId);
        inProgress.Assign(MemberId);
        inProgress.AssignCategory(ReleaseCategoryId);
        inProgress.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-1))));
        inProgress.Estimate(EffortEstimate.Create(3));
        inProgress.SetPlanningFactors(PlanningFactors.Create(5, 5, 4, 3));
        inProgress.AssignSprint(ActiveSprintId);
        inProgress.MoveToReady();
        inProgress.Start();
        inProgress.AddTag("analytics");
        inProgress.AddTag("risk");

        var completed = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000005"),
            ProjectId,
            "Configure workspace access");
        completed.RecordCreator(OwnerId);
        completed.Assign(OwnerId);
        completed.AssignCategory(OperationsCategoryId);
        completed.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-3))));
        completed.Estimate(EffortEstimate.Create(2));
        completed.SetPlanningFactors(PlanningFactors.Create(3, 3, 2, 2));
        completed.AssignSprint(ActiveSprintId);
        completed.MoveToReady();
        completed.Start();
        completed.Complete(DateTimeOffset.UtcNow.AddDays(-1));
        completed.AddTag("security");

        var sprintProject = Project.Create(
            SprintProjectId,
            "Client onboarding sprint",
            "Short delivery project used by workspace-wide reports.",
            WorkspaceId);
        sprintProject.SetTargetDate(
            DueDate.Create(DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(1))));
        var onboardingSprint = sprintProject.AddSprint(
            OnboardingSprintId,
            "Client activation sprint",
            "Finish onboarding assets and handover notes before the client launch date.",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)));
        onboardingSprint.Start();
        var sprintTask = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000006"),
            SprintProjectId,
            "Confirm client welcome pack");
        sprintTask.RecordCreator(ManagerId);
        sprintTask.Assign(MemberId);
        sprintTask.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(1))));
        sprintTask.Estimate(EffortEstimate.Create(2));
        sprintTask.SetPlanningFactors(PlanningFactors.Create(4, 4, 2, 2));
        sprintTask.AssignSprint(OnboardingSprintId);
        sprintTask.MoveToReady();
        sprintTask.Start();
        sprintTask.AddTag("client");
        sprintTask.AddTag("notification");

        var closedProject = Project.Create(
            ClosedProjectId,
            "Discovery phase",
            "Archived project used to demonstrate completed project reporting.",
            WorkspaceId);
        closedProject.SetTargetDate(
            DueDate.Create(DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(-7))));
        var discoveryOne = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000007"),
            ClosedProjectId,
            "Interview stakeholders");
        discoveryOne.RecordCreator(OwnerId);
        discoveryOne.Assign(ManagerId);
        discoveryOne.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-12))));
        discoveryOne.Estimate(EffortEstimate.Create(3));
        discoveryOne.SetPlanningFactors(PlanningFactors.Create(3, 3, 2, 2));
        discoveryOne.MoveToReady();
        discoveryOne.Start();
        discoveryOne.Complete(DateTimeOffset.UtcNow.AddDays(-10));
        discoveryOne.AddTag("discovery");

        var discoveryTwo = TaskItem.Create(
            Guid.Parse("c3100000-0000-0000-0000-000000000008"),
            ClosedProjectId,
            "Publish discovery report");
        discoveryTwo.RecordCreator(ManagerId);
        discoveryTwo.Assign(OwnerId);
        discoveryTwo.Schedule(DueDate.Create(DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-8))));
        discoveryTwo.Estimate(EffortEstimate.Create(2));
        discoveryTwo.SetPlanningFactors(PlanningFactors.Create(4, 3, 3, 2));
        discoveryTwo.MoveToReady();
        discoveryTwo.Start();
        discoveryTwo.Complete(DateTimeOffset.UtcNow.AddDays(-6));
        discoveryTwo.AddTag("report");
        closedProject.Archive(DateTimeOffset.UtcNow.AddDays(-5));

        context.AddRange(
            project,
            sprintProject,
            closedProject,
            backlog,
            ready,
            blocked,
            inProgress,
            completed,
            sprintTask,
            discoveryOne,
            discoveryTwo);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddMissingCredentialAsync(
        TodoAppDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (await context.UserCredentials.AnyAsync(
                credential => credential.UserId == userId,
                cancellationToken))
        {
            return;
        }

        await context.UserCredentials.AddAsync(
            new UserCredential(
                userId,
                DevelopmentPasswordHasher.Hash(Password)),
            cancellationToken);
    }

    private static async Task EnsureUserAsync(
        TodoAppDbContext context,
        Guid userId,
        string displayName,
        string email,
        CancellationToken cancellationToken)
    {
        var user = await context.UserProfiles.SingleOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);
        if (user is null)
        {
            context.UserProfiles.Add(UserProfile.Create(userId, displayName, email));
        }
    }

    private static void EnsureMember(Workspace workspace, Guid userId, WorkspaceRole role)
    {
        if (!workspace.Memberships.Any(member => member.UserId == userId))
        {
            workspace.AddMember(OwnerId, userId, role);
        }
    }

    private static async Task SeedPersonalTodosAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.PersonalTodos.AnyAsync(
                todo => todo.Id == OwnerTodoId,
                cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var ownerTodo = PersonalTodo.Create(
            OwnerTodoId,
            OwnerId,
            "Review platform admin dashboard",
            today,
            "Check the Platform page's workspace list before the demo.",
            TodoPriority.Medium,
            now.AddDays(-1));

        var managerTodo = PersonalTodo.Create(
            ManagerTodoId,
            ManagerId,
            "Confirm sprint capacity for next week",
            today,
            null,
            TodoPriority.High,
            now.AddHours(-6));

        var memberTodo = PersonalTodo.Create(
            MemberTodoId,
            MemberId,
            "Write release notes draft",
            today,
            "Keep it short - bullet points only.",
            TodoPriority.Critical,
            now.AddDays(-2));
        memberTodo.AddComment(
            MemberTodoCommentId,
            "Draft is in the shared doc, ready for review.",
            now.AddDays(-1));

        context.AddRange(ownerTodo, managerTodo, memberTodo);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDailyRoutineAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.DailyRoutines.AnyAsync(
                routine => routine.Id == DailyRoutineId,
                cancellationToken))
        {
            return;
        }

        var routine = DailyRoutine.Create(
            DailyRoutineId,
            MemberId,
            "Daily standup notes",
            "Summarise yesterday's progress and today's plan.",
            TodoPriority.Medium,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
            null,
            DateTimeOffset.UtcNow.AddDays(-14));

        context.DailyRoutines.Add(routine);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPendingInvitationAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.WorkspaceInvitations.AnyAsync(
                invitation => invitation.Id == PendingInvitationId,
                cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var invitation = WorkspaceInvitation.Create(
            PendingInvitationId,
            WorkspaceId,
            "New Hire",
            "new.hire@example.com",
            WorkspaceRole.Member,
            OwnerId,
            Guid.NewGuid().ToString("N"),
            now.AddDays(-1),
            now.AddDays(6));

        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }
}
