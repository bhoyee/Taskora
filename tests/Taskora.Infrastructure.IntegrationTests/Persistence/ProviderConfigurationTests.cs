using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Infrastructure.Persistence;

namespace TodoApp.Infrastructure.IntegrationTests.Persistence;

// Verifies that AddInfrastructure selects the correct EF Core database
// provider (Sqlite/SqlServer/Postgres) from configuration and correctly
// normalizes real-world connection string formats (e.g. Neon/Render pastes).
public sealed class ProviderConfigurationTests
{
    [Theory]
    [InlineData("Sqlite", "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Postgres", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("PostgreSQL", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Npgsql", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void AddInfrastructure_SelectsConfiguredProvider(
        string provider,
        string expectedProviderName)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:TodoApp"] = ConnectionStringFor(provider)
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<TodoAppDbContext>();

        Assert.Equal(expectedProviderName, context.Database.ProviderName);
    }

    [Fact]
    public void AddInfrastructure_AcceptsNeonPostgresUrl()
    {
        AssertPostgresProvider(
            "postgresql://taskora_user:taskora_password@example.neon.tech/neondb?sslmode=require&channel_binding=require");
    }

    // Covers connection strings pasted with surrounding quotes, or still
    // prefixed with their env-var name (as commonly copied from Render),
    // both of which must be cleaned before reaching Npgsql.
    [Theory]
    [InlineData("\"postgresql://taskora_user:taskora_password@example.neon.tech/neondb?sslmode=require\"")]
    [InlineData("ConnectionStrings__TodoApp=postgresql://taskora_user:taskora_password@example.neon.tech/neondb?sslmode=require")]
    public void AddInfrastructure_CleansCommonRenderConnectionStringPastes(
        string connectionString)
    {
        AssertPostgresProvider(connectionString);
    }

    // Builds the DI container for the given connection string and asserts
    // the resolved DbContext resolves to the Npgsql provider.
    private static void AssertPostgresProvider(
        string connectionString)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["ConnectionStrings:TodoApp"] = connectionString
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<TodoAppDbContext>();

        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            context.Database.ProviderName);
    }

    // Builds a provider-appropriate connection string for the
    // AddInfrastructure_SelectsConfiguredProvider theory cases.
    private static string ConnectionStringFor(string provider)
    {
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return "Data Source=:memory:";
        }

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return "Server=(localdb)\\mssqllocaldb;Database=TodoApp;Trusted_Connection=True;";
        }

        return "Host=localhost;Port=5432;Database=taskora;Username=taskora;Password=taskora";
    }
}
