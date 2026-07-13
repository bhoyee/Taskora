using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Persistence;
using TodoApp.Infrastructure.Persistence.Seeding;

namespace TodoApp.Infrastructure.IntegrationTests.Persistence;

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

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

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
            Assert.Equal(1, await readContext.Projects.CountAsync());
            Assert.Equal(3, await readContext.Tasks.CountAsync());
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

        Assert.Equal(1, await context.Projects.CountAsync());
        Assert.Equal(3, await context.Tasks.CountAsync());
    }
}
