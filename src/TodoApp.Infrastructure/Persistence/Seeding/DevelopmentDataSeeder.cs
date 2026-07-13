using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Seeding;

public static class DevelopmentDataSeeder
{
    public static readonly Guid OwnerId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId =
        Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid WorkspaceId =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    public const string DemoOwnerEmail = "jadesola@example.com";
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

    public static async Task SeedAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.UserProfiles.AnyAsync(cancellationToken))
        {
            var owner = UserProfile.Create(
                OwnerId,
                "Jadesola Aliu",
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

        await AddMissingCredentialAsync(context, OwnerId, cancellationToken);
        await AddMissingCredentialAsync(context, ManagerId, cancellationToken);
        await AddMissingCredentialAsync(context, MemberId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

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

internal static class DevelopmentPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

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
