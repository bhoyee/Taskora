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
    public const string DemoPassword = "Portfolio123!";

    private static readonly Guid ProjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.UserProfiles.AnyAsync(cancellationToken))
        {
            var owner = UserProfile.Create(
                OwnerId,
                "Jadesola Aliu",
                "jadesola@example.com");
            var manager = UserProfile.Create(
                ManagerId,
                "Delivery Manager",
                "manager@example.com");
            var member = UserProfile.Create(
                MemberId,
                "Team Member",
                "member@example.com");
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

        var backlog = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            ProjectId,
            "Review portfolio requirements");
        var ready = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            ProjectId,
            "Prepare deployment checklist");
        ready.SetPlanningFactors(
            PlanningFactors.Create(4, 4, 3, 3));
        ready.MoveToReady();
        var blocked = TaskItem.Create(
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            ProjectId,
            "Publish production release");
        blocked.SetPlanningFactors(
            PlanningFactors.Create(5, 5, 4, 3));
        blocked.MoveToReady();
        blocked.Start();
        blocked.Block("Waiting for deployment approval");

        context.AddRange(project, backlog, ready, blocked);
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
