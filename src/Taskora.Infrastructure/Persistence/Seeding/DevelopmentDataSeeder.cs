using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

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
    public static readonly Guid WorkspaceId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    public const string DemoOwnerEmail = "salisu.adeboye@gmail.com";
    public const string DemoManagerEmail = "manager@example.com";
    public const string DemoMemberEmail = "member@example.com";
    public const string DemoPassword = "Portfolio123!";

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
        // Step 1: create the demo workspace and its three users (owner,
        // manager, member) the first time the database is empty.
        if (!await context.UserProfiles.AnyAsync(cancellationToken))
        {
            var owner = UserProfile.Create(
                OwnerId,
                "Salisu Adeboye",
                DemoOwnerEmail);
            var manager = UserProfile.Create(
                ManagerId,
                "Delivery Manager",
                DemoManagerEmail);
            var member = UserProfile.Create(
                MemberId,
                "Team Member",
                DemoMemberEmail);
            var workspace = Workspace.Create(
                WorkspaceId,
                "Portfolio team",
                OwnerId);
            workspace.AddMember(
                OwnerId,
                ManagerId,
                WorkspaceRole.Manager);
            workspace.AddMember(
                OwnerId,
                MemberId,
                WorkspaceRole.Member);
            context.AddRange(owner, manager, member, workspace);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Users already exist (e.g. from a previous seed run) — just
            // keep the demo owner's email address in sync with the constant
            // above in case it was changed between deployments.
            var owner = await context.UserProfiles
                .SingleOrDefaultAsync(
                    user => user.Id == OwnerId,
                    cancellationToken);
            owner?.UpdateEmail(DemoOwnerEmail);
        }

        // Step 2: ensure each demo user has a login credential, even if the
        // user rows themselves were seeded in an earlier run.
        await AddMissingCredentialAsync(context, OwnerId, cancellationToken);
        await AddMissingCredentialAsync(context, ManagerId, cancellationToken);
        await AddMissingCredentialAsync(context, MemberId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Step 3: only seed demo projects/tasks once — if any project
        // already exists, assume the rest of the demo data is in place too.
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        // Step 4: build the primary "Portfolio launch" project with two
        // sprints and a spread of tasks across backlog/ready/blocked/
        // in-progress/completed states, to exercise the board and reports.
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
