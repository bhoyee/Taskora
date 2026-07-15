using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(
            Path.GetTempPath(),
            $"todoapp-api-{Guid.NewGuid():N}.db");

    public RecordingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TodoApp"] =
                        $"Data Source={_databasePath}",
                    ["Database:Provider"] = "Sqlite",
                    ["Email:Smtp:Enabled"] = "false",
                    ["Administration:SuperAdminEmails:0"] =
                        "salisu.adeboye@gmail.com"
                });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationEmailSender>();
            services.AddSingleton<INotificationEmailSender>(EmailSender);
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "30000000-0000-0000-0000-000000000001");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

public sealed class RecordingEmailSender : INotificationEmailSender
{
    private readonly List<NotificationEmailMessage> _messages = [];

    public IReadOnlyList<NotificationEmailMessage> Messages => _messages;

    public Task SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
