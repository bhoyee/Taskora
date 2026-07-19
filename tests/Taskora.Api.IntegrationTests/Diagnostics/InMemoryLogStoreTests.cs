using TodoApp.Api.Diagnostics;
using Xunit;

namespace TodoApp.Api.IntegrationTests.Diagnostics;

public sealed class InMemoryLogStoreTests
{
    [Fact]
    public void Recent_PrunesEntriesOutsideRetentionWindow()
    {
        var store = new InMemoryLogStore(maxEntries: 10, retentionDays: 30);
        store.Add(new OperationLogEntry(
            DateTimeOffset.UtcNow.AddDays(-31),
            "Information",
            "Old",
            "Expired log",
            null,
            null,
            null));
        store.Add(new OperationLogEntry(
            DateTimeOffset.UtcNow,
            "Information",
            "Current",
            "Active log",
            null,
            null,
            null));

        var entry = Assert.Single(store.Recent());
        Assert.Equal("Active log", entry.Message);
    }

    [Fact]
    public void Recent_PrunesEntriesBeyondMaxEntryCount()
    {
        var store = new InMemoryLogStore(maxEntries: 2, retentionDays: 30);
        store.Add(CreateEntry("First"));
        store.Add(CreateEntry("Second"));
        store.Add(CreateEntry("Third"));

        Assert.Equal(
            ["Third", "Second"],
            store.Recent().Select(entry => entry.Message));
    }

    private static OperationLogEntry CreateEntry(string message) =>
        new(
            DateTimeOffset.UtcNow,
            "Information",
            "Test",
            message,
            null,
            null,
            null);
}
