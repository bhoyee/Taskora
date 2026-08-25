using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TodoApp.Application.Projects;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence.Seeding;

/// <summary>
/// Populates a fresh database with a fixed set of demo data (a workspace,
/// three users with well-known credentials, and a handful of projects,
/// sprints, and tasks in various states) so local development and portfolio
/// demos have something realistic to look at. Intended to be invoked once at
/// application startup in non-production environments; it is idempotent —
/// each stage checks whether its data already exists before inserting, so
/// running it repeatedly against the same database is a no-op after the
/// first run (aside from keeping the owner's demo email in sync).
/// </summary>
public static class DevelopmentDataSeeder
{
    // Fixed (non-random) GUIDs and demo credentials below so seeded data is
    // stable across runs and reseeds, and so demo login credentials are
    // predictable for local testing/demos.
    public static readonly Guid OwnerId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId =
        Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid SuperAdminId =
        Guid.Parse("30000000-0000-0000-0000-000000000004");
    public static readonly Guid WorkspaceId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    public const string DemoOwnerEmail = "owner@example.com";
    public const string DemoManagerEmail = "manager@example.com";
    public const string DemoMemberEmail = "member@example.com";
    public const string DemoSuperAdminEmail = "superadmin@example.com";
    public const string DemoPassword = "Portfolio123!";

    private static readonly Guid OwnerTodoId =
        Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid ManagerTodoId =
        Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid MemberTodoId =
        Guid.Parse("80000000-0000-0000-0000-000000000003");
    private static readonly Guid MemberTodoCommentId =
        Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid DailyRoutineId =
        Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid PendingInvitationId =
        Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private static readonly Guid ProjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SprintProjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid ClosedProjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid OperationsCategoryId =
        Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ReleaseCategoryId =
        Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid ActiveSprintId =
        Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid PlannedSprintId =
        Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid OnboardingSprintId =
        Guid.Parse("70000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Seeds demo users/workspace and demo projects/tasks if they don't
    /// already exist. Safe to call on every startup in development —
    /// each section below is guarded by an existence check.
    /// </summary>
    public static async Task SeedAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        // Step 1: ensure each demo persona exists, checked individually (rather
        // than an all-or-nothing "no users at all" gate) so a persona added in a
        // later release — e.g. SuperAdminId below — still gets created against a
        // database that was already seeded by an earlier version of this method.
        await EnsureUserAsync(context, OwnerId, "Salisu Adeboye", DemoOwnerEmail, cancellationToken);
        await EnsureUserAsync(context, ManagerId, "Delivery Manager", DemoManagerEmail, cancellationToken);
        await EnsureUserAsync(context, MemberId, "Team Member", DemoMemberEmail, cancellationToken);
        await EnsureUserAsync(context, SuperAdminId, "Platform Admin", DemoSuperAdminEmail, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Step 2: ensure the demo workspace exists and every persona above is a
        // member of it (loading "_memberships" explicitly since it's a non-owned
        // navigation EF won't eager-load without an Include).
        var workspace = await context.Workspaces
            .Include("_memberships")
            .SingleOrDefaultAsync(w => w.Id == WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            workspace = Workspace.Create(WorkspaceId, "Portfolio team", OwnerId);
            context.Workspaces.Add(workspace);
        }
        EnsureMember(workspace, ManagerId, WorkspaceRole.Manager);
        EnsureMember(workspace, MemberId, WorkspaceRole.Member);
        // Super Admin gets the same baseline Member role here — their extra
        // access to Platform/Operations/Backups comes purely from their email
        // being on the SuperAdminEmails allowlist, not from anything workspace-
        // specific, so the demo should not special-case them at this layer.
        EnsureMember(workspace, SuperAdminId, WorkspaceRole.Member);
        await context.SaveChangesAsync(cancellationToken);

        // Step 3: ensure each demo user has a login credential, even if the
        // user rows themselves were seeded in an earlier run.
        await AddMissingCredentialAsync(context, OwnerId, cancellationToken);
        await AddMissingCredentialAsync(context, ManagerId, cancellationToken);
        await AddMissingCredentialAsync(context, MemberId, cancellationToken);
        await AddMissingCredentialAsync(context, SuperAdminId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Step 4: only seed demo projects/tasks once — if any project already
        // exists, assume this section already ran (the sections below it each
        // have their own independent existence checks, so they still run even
        // when this one is skipped against an already-seeded database).
        if (!await context.Projects.AnyAsync(cancellationToken))
        {
            await SeedProjectsAndTasksAsync(context, cancellationToken);
        }

        // Step 5 (new): a couple of My Day todos across personas, including
        // one with a comment, so the My Day page isn't empty for the demo.
        await SeedPersonalTodosAsync(context, cancellationToken);

        // Step 6 (new): a recurring daily routine, so the Routines page has
        // something to show.
        await SeedDailyRoutineAsync(context, cancellationToken);

        // Step 7 (new): a pending team invitation, so the Team page's
        // "Pending invitations" section has an example when viewed as
        // Owner/Manager.
        await SeedPendingInvitationAsync(context, cancellationToken);
    }

    // Builds the primary "Portfolio launch" project with two sprints and a
    // spread of tasks across backlog/ready/blocked/in-progress/completed
    // states, plus two supporting projects, to exercise the board and reports.
    private static async Task SeedProjectsAndTasksAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            "Demonstration project for local development.",
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
            "Prepare the final Azure deployment story, production settings, and portfolio walkthrough.",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)));

        var backlog = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
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
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            OwnerId,
            "Confirm acceptance criteria before delivery planning.",
            DateTimeOffset.UtcNow.AddDays(-4));

        var ready = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
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
            Guid.Parse("60000000-0000-0000-0000-000000000002"),
            ManagerId,
            "Checklist is ready for final review.",
            DateTimeOffset.UtcNow.AddDays(-2));

        var blocked = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
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
            Guid.Parse("60000000-0000-0000-0000-000000000003"),
            OwnerId,
            "Approval is the current release risk.",
            DateTimeOffset.UtcNow.AddDays(-1));

        var inProgress = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000004"),
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
            Guid.Parse("20000000-0000-0000-0000-000000000005"),
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

        // Step 5: a short second project with its own sprint, used to
        // exercise workspace-wide (cross-project) reporting.
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
            Guid.Parse("20000000-0000-0000-0000-000000000006"),
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

        // Step 6: a fully completed and archived project, so completed/
        // archived-project reporting has demo data to show as well.
        var closedProject = Project.Create(
            ClosedProjectId,
            "Discovery phase",
            "Archived project used to demonstrate completed project reporting.",
            WorkspaceId);
        closedProject.SetTargetDate(
            DueDate.Create(DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(-7))));
        var discoveryOne = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000007"),
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
            Guid.Parse("20000000-0000-0000-0000-000000000008"),
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

    /// <summary>
    /// Deletes the mutable "content" tier of the demo data (projects/sprints/
    /// tasks, My Day todos, the daily routine, and the pending invitation) for
    /// the fixed demo ids, then re-seeds it via <see cref="SeedAsync"/> —
    /// restoring a pristine demo without touching the identity tier (the four
    /// user accounts, their credentials, the workspace, or memberships), so
    /// existing demo logins keep working across a reset. Project deletion goes
    /// through the same <see cref="DeleteProjectHandler"/>/<c>HasAdministrativeBypass</c>
    /// path the Platform page's hard-delete already uses, rather than
    /// hand-rolling EF cascade deletes for entities with complex FK cleanup
    /// (e.g. task dependencies).
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

    // Inserts a demo credential (using the shared DemoPassword) for the
    // given user if one doesn't already exist.
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
                DevelopmentPasswordHasher.Hash(DemoPassword)),
            cancellationToken);
    }

    // Creates the demo user if missing, or keeps its email in sync with the
    // constant above if it already exists (e.g. after this seeder's own
    // address rename between deployments).
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
            return;
        }

        user.UpdateEmail(email);
    }

    // Adds userId as a member of the demo workspace (acting as the demo
    // owner) unless they already belong to it.
    private static void EnsureMember(Workspace workspace, Guid userId, WorkspaceRole role)
    {
        if (!workspace.Memberships.Any(member => member.UserId == userId))
        {
            workspace.AddMember(OwnerId, userId, role);
        }
    }

    // Seeds a couple of My Day todos across personas — one with a comment —
    // so the My Day page has content to show off. Gated on the owner's todo
    // existing so it still runs against a database seeded before this section
    // was added.
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

    // Seeds one recurring daily routine so the Routines page has content.
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

    // Seeds one pending team invitation so the Team page's "Pending
    // invitations" section has an example when viewed as Owner/Manager.
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

/// <summary>
/// Minimal PBKDF2 password hasher used only to produce demo user credentials
/// during development seeding. Not intended as the production password
/// hashing implementation.
/// </summary>
internal static class DevelopmentPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>Hashes a password with a random salt, returning "iterations.salt.hash" (base64 salt/hash).</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
