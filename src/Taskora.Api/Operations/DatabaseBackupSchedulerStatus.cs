namespace TodoApp.Api.Operations;

public sealed class DatabaseBackupSchedulerStatus
{
    private readonly object _sync = new();
    private DatabaseBackupSnapshot _snapshot = DatabaseBackupSnapshot.NotStarted();

    public DatabaseBackupSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public void Disabled()
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = false,
                Status = "Disabled",
                NextRunAt = null
            };
        }
    }

    public void Scheduled(int intervalHours, DateTimeOffset nextRunAt)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = true,
                Status = "Scheduled",
                IntervalHours = intervalHours,
                NextRunAt = nextRunAt
            };
        }
    }

    public void Started(DateTimeOffset startedAt)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = true,
                Status = "Running",
                LastRunStartedAt = startedAt,
                LastError = null
            };
        }
    }

    public void Completed(
        DateTimeOffset completedAt,
        DateTimeOffset nextRunAt,
        DatabaseBackupFile backup)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = true,
                Status = "Healthy",
                LastRunCompletedAt = completedAt,
                NextRunAt = nextRunAt,
                LastBackupFileName = backup.FileName,
                LastBackupSizeBytes = backup.SizeBytes,
                LastError = null
            };
        }
    }

    public void Failed(
        DateTimeOffset failedAt,
        DateTimeOffset nextRunAt,
        Exception exception)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = true,
                Status = "Failed",
                LastRunCompletedAt = failedAt,
                NextRunAt = nextRunAt,
                LastError = exception.Message
            };
        }
    }
}

public sealed record DatabaseBackupSnapshot(
    bool Enabled,
    string Status,
    int IntervalHours,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    string? LastBackupFileName,
    long LastBackupSizeBytes,
    string? LastError)
{
    public static DatabaseBackupSnapshot NotStarted() =>
        new(
            false,
            "Not started",
            24,
            null,
            null,
            null,
            null,
            0,
            null);
}
