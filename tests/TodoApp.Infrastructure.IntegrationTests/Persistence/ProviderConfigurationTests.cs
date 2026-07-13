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
    public void AddInfrastructure_SelectsConfiguredProvider(
        string provider,
        string expectedProviderName)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:TodoApp"] = provider == "Sqlite"
                ? "Data Source=:memory:"
                : "Server=(localdb)\\mssqllocaldb;Database=TodoApp;Trusted_Connection=True;"
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
}
