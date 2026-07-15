using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TodoApp.Infrastructure.Persistence;

public sealed class TodoAppDbContextFactory
    : IDesignTimeDbContextFactory<TodoAppDbContext>
{
    public TodoAppDbContext CreateDbContext(string[] args)
    {
        var provider =
            Environment.GetEnvironmentVariable("Database__Provider") ??
            "Sqlite";
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__TodoApp") ??
            "Data Source=todoapp.db";
        var builder = new DbContextOptionsBuilder<TodoAppDbContext>();

        if (provider.Equals(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure());
        }
        else if (IsPostgres(provider))
        {
            builder.UseNpgsql(
                connectionString,
                postgres => postgres.EnableRetryOnFailure());
        }
        else
        {
            builder.UseSqlite(connectionString);
        }

        return new TodoAppDbContext(builder.Options);
    }

    private static bool IsPostgres(string provider) =>
        provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase);
}
