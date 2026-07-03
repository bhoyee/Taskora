using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Projects;
using TodoApp.Domain.Tasks;

namespace TodoApp.Infrastructure.Persistence.Seeding;

public static class DevelopmentDataSeeder
{
    private static readonly Guid ProjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(
        TodoAppDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var project = Project.Create(
            ProjectId,
            "Portfolio launch",
            "Demonstration project for local development.");
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
}
