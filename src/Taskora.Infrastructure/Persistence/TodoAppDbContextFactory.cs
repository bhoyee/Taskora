using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the <c>dotnet ef</c> tooling (e.g.
/// `dotnet ef migrations add`, `dotnet ef database update`) to construct a
/// <see cref="TodoAppDbContext"/> outside of the normal application startup
/// path, where the DI container and its configuration are not available.
/// The provider and connection string are read directly from environment
/// variables (mirroring the names ASP.NET Core configuration would bind to)
/// so the same environment settings used to run the app can be reused for
/// migration commands, defaulting to a local SQLite database when unset.
/// </summary>
public sealed class TodoAppDbContextFactory
    : IDesignTimeDbContextFactory<TodoAppDbContext>
{
    /// <summary>Builds a <see cref="TodoAppDbContext"/> configured from environment variables, for use by EF Core design-time tools.</summary>
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
        // The app supports SQLite (dev) and Postgres (prod) with a single
        // shared migration history; provider-specific type metadata can
        // otherwise trigger a spurious "pending model changes" warning here.
        builder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

        // Provider selection mirrors DependencyInjection.AddInfrastructure
        // so migrations are generated/applied against the same provider the
        // running app would use.
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
