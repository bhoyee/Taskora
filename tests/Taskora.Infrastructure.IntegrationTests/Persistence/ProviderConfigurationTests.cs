using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Infrastructure.Persistence;

namespace TodoApp.Infrastructure.IntegrationTests.Persistence;

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
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["ConnectionStrings:TodoApp"] =
                "postgresql://taskora_user:taskora_password@example.neon.tech/neondb?sslmode=require&channel_binding=require"
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
