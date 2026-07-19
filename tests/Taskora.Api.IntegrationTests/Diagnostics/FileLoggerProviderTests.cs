using System.Text.Json;
using Microsoft.Extensions.Logging;
using TodoApp.Api.Diagnostics;
using Xunit;

namespace TodoApp.Api.IntegrationTests.Diagnostics;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void Logger_WritesJsonLineWithCorrelationId()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"taskora-logs-{Guid.NewGuid():N}");
        using var provider = new FileLoggerProvider(new FileLoggerOptions
        {
            Enabled = true,
            Directory = directory,
            RetentionDays = 7
        });
        var logger = provider.CreateLogger("Taskora.Tests");

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = "trace-123"
               }))
        {
            logger.LogInformation(
                "Created project {ProjectId}",
                "project-1");
        }

        var file = Assert.Single(Directory.GetFiles(directory, "*.jsonl"));
        var line = Assert.Single(File.ReadAllLines(file));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        Assert.Equal("Information", root.GetProperty("Level").GetString());
        Assert.Equal("Taskora.Tests", root.GetProperty("Category").GetString());
        Assert.Equal("trace-123", root.GetProperty("CorrelationId").GetString());
        Assert.Contains(
            "project-1",
            root.GetProperty("Message").GetString(),
            StringComparison.Ordinal);

        Directory.Delete(directory, recursive: true);
    }
}
