using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TodoApp.Application.Abstractions;

namespace TodoApp.Api.IntegrationTests;

// WebApplicationFactory used by every integration test class in this project. Boots the
// real API host against a throwaway per-instance SQLite database, disables outbound SMTP
// in favor of an in-memory RecordingEmailSender, seeds a fixed super-admin email, and
// authenticates outgoing requests as the seeded development owner via the X-User-Id header.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(
            Path.GetTempPath(),
            $"todoapp-api-{Guid.NewGuid():N}.db");

    public RecordingEmailSender EmailSender { get; } = new();

    // Points the host at a unique temp SQLite file, turns off real SMTP delivery, and
    // swaps in RecordingEmailSender so tests can assert on emails without sending any.
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

    // Every client created by this factory is pre-authenticated as the seeded
    // development owner unless the test explicitly removes/overrides the header.
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "30000000-0000-0000-0000-000000000001");
    }

    // Cleans up the per-instance SQLite file so temp disk usage doesn't accumulate
    // across test runs.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

// Fake INotificationEmailSender used in place of real SMTP delivery. Captures every
// message so tests can inspect subjects/bodies (e.g. to scrape a password reset code)
// without any network I/O.
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
