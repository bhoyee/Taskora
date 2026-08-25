namespace TodoApp.Api.Operations;

/// <summary>
/// Thread-safe, in-memory holder for the database backup scheduler's latest
/// state, exposed to the Operations dashboard/API. Each mutator swaps the
/// immutable <see cref="DatabaseBackupSnapshot"/> under a lock so readers
/// never see a partially updated snapshot.
/// </summary>
public sealed class DatabaseBackupSchedulerStatus
{
    private readonly object _sync = new();
    private DatabaseBackupSnapshot _snapshot = DatabaseBackupSnapshot.NotStarted();

    /// <summary>The current point-in-time snapshot of the backup scheduler's status.</summary>
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

    /// <summary>Records that automatic backups are turned off (no scheduled next run).</summary>
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

    /// <summary>Records that backups are enabled and the next run has been scheduled.</summary>
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

    /// <summary>Records that a backup run has just started, clearing any prior error.</summary>
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

    /// <summary>Records a successful backup run, its output file, and when the next run is due.</summary>
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

    /// <summary>Records a failed backup run and its error message, and reschedules the next attempt.</summary>
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

/// <summary>Immutable point-in-time view of the database backup scheduler's configuration, last run, and outcome.</summary>
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
    // Default snapshot before the scheduler has run for the first time.
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
