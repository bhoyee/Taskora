using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

namespace TodoApp.Infrastructure.IntegrationTests.Persistence;

// Verifies EF Core migrations apply cleanly and that development seed data
// is written and re-applied correctly against a real SQLite database.
public sealed class MigrationAndSeedTests
{
    [Fact]
    public async Task Migrate_OnCleanDatabase_AppliesInitialSchema()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TodoAppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new TodoAppDbContext(options);

        // Migrating twice confirms MigrateAsync is idempotent/safe to re-run.
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

        // Confirms the initial migration (by name suffix) was applied.
        Assert.Contains(
            await context.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith(
                "_InitialPersistence",
                StringComparison.Ordinal));
        Assert.True(await context.Projects.AnyAsync() == false);
    }

    [Fact]
    public async Task FileDatabase_AfterContextRestart_PreservesData()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"todoapp-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<TodoAppDbContext>()
                .UseSqlite(
                    $"Data Source={databasePath};Pooling=False")
                .Options;

            await using (var writeContext = new TodoAppDbContext(options))
            {
                await writeContext.Database.MigrateAsync();
                await DevelopmentDataSeeder.SeedAsync(
                    writeContext,
                    CancellationToken.None);
            }

            await using var readContext = new TodoAppDbContext(options);
            // Expected counts come from the fixed DevelopmentDataSeeder dataset.
            Assert.Equal(3, await readContext.Projects.CountAsync());
            Assert.Equal(8, await readContext.Tasks.CountAsync());
            Assert.Equal(2, await readContext.ProjectCategories.CountAsync());
            Assert.Equal(3, await readContext.UserCredentials.CountAsync());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task SeedDevelopmentData_WhenCalledTwice_IsIdempotent()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TodoAppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new TodoAppDbContext(options);
        await context.Database.MigrateAsync();

        await DevelopmentDataSeeder.SeedAsync(
            context,
            CancellationToken.None);
        await DevelopmentDataSeeder.SeedAsync(
            context,
            CancellationToken.None);

        Assert.Equal(3, await context.Projects.CountAsync());
        Assert.Equal(8, await context.Tasks.CountAsync());
        Assert.Equal(2, await context.ProjectCategories.CountAsync());
        Assert.True(await context.TaskActivities.AnyAsync());
    }

    // Guards against a repeat of a real incident: PublicDemoSeeder must never
    // read or write any id/email DevelopmentDataSeeder uses, even when both
    // run against the same database (as they can in production, since each
    // is gated by its own independent config flag).
    [Fact]
    public async Task PublicDemoSeeder_NeverTouchesDevelopmentDataSeederIdentities()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TodoAppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new TodoAppDbContext(options);
        await context.Database.MigrateAsync();

        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        await PublicDemoSeeder.SeedAsync(context, CancellationToken.None);

        var devOwner = await context.UserProfiles.SingleAsync(
            u => u.Id == DevelopmentDataSeeder.OwnerId);
        Assert.Equal(DevelopmentDataSeeder.DemoOwnerEmail, devOwner.Email);
        Assert.NotEqual(PublicDemoSeeder.OwnerEmail, devOwner.Email);

        var demoOwner = await context.UserProfiles.SingleAsync(
            u => u.Id == PublicDemoSeeder.OwnerId);
        Assert.Equal(PublicDemoSeeder.OwnerEmail, demoOwner.Email);

        Assert.NotEqual(DevelopmentDataSeeder.OwnerId, PublicDemoSeeder.OwnerId);
        Assert.NotEqual(DevelopmentDataSeeder.WorkspaceId, PublicDemoSeeder.WorkspaceId);

        // 3 projects from each seeder, none shared - 6 total, not 3.
        Assert.Equal(6, await context.Projects.CountAsync());
        // 3 workspaces exist: DevelopmentDataSeeder's, PublicDemoSeeder's, and
        // the one PublicDemoSeeder's own members implicitly get nothing extra
        // from - confirmed by member count staying scoped per workspace.
        var devWorkspace = await context.Workspaces
            .Include("_memberships")
            .SingleAsync(w => w.Id == DevelopmentDataSeeder.WorkspaceId);
        Assert.DoesNotContain(
            devWorkspace.Memberships,
            member => member.UserId == PublicDemoSeeder.SuperAdminId
                || member.UserId == PublicDemoSeeder.ManagerId
                || member.UserId == PublicDemoSeeder.MemberId);
    }

    [Fact]
    public async Task PublicDemoSeeder_WhenCalledTwice_IsIdempotent()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TodoAppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new TodoAppDbContext(options);
        await context.Database.MigrateAsync();

        await PublicDemoSeeder.SeedAsync(context, CancellationToken.None);
        await PublicDemoSeeder.SeedAsync(context, CancellationToken.None);

        Assert.Equal(3, await context.Projects.CountAsync());
        Assert.Equal(8, await context.Tasks.CountAsync());
        Assert.Equal(4, await context.UserCredentials.CountAsync());
        Assert.Equal(3, await context.PersonalTodos.CountAsync());
        Assert.Equal(1, await context.DailyRoutines.CountAsync());
        Assert.Equal(1, await context.WorkspaceInvitations.CountAsync());
    }
}
