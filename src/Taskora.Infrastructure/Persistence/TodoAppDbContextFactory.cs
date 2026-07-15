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
            ConnectionStringNormalizer.ForProvider(
                provider,
                Environment.GetEnvironmentVariable(
                    "ConnectionStrings__TodoApp") ??
                "Data Source=todoapp.db");
        var builder = new DbContextOptionsBuilder<TodoAppDbContext>();

        if (provider.Equals(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure());
        }
        else if (ConnectionStringNormalizer.IsPostgres(provider))
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

}
