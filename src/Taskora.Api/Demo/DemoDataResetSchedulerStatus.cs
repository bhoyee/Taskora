namespace TodoApp.Api.Demo;

/// <summary>
/// Thread-safe, in-memory holder for the demo-data reset scheduler's latest
/// state, exposed to the Operations dashboard/API. Mirrors
/// <see cref="TodoApp.Api.Notifications.DueDateReminderSchedulerStatus"/>'s
/// snapshot-under-lock shape.
/// </summary>
public sealed class DemoDataResetSchedulerStatus
{
    private readonly object _sync = new();
    private DemoDataResetSnapshot _snapshot = DemoDataResetSnapshot.NotStarted();

    /// <summary>The current point-in-time snapshot of the reset scheduler's status.</summary>
    public DemoDataResetSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>Records the scheduler's enabled/disabled state, its run interval, and when it will next run.</summary>
    public void Configure(
        bool enabled,
        TimeSpan interval,
        DateTimeOffset? nextRunAt)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Enabled = enabled,
                IntervalHours = Math.Max(1, (int)Math.Round(interval.TotalHours)),
                NextRunAt = nextRunAt,
                Status = enabled ? "Waiting" : "Disabled"
            };
        }
    }

    /// <summary>Records that a reset run has just started, clearing any prior error.</summary>
    public void MarkRunning(DateTimeOffset startedAt)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = "Running",
                LastRunStartedAt = startedAt,
                LastError = null
            };
        }
    }

    /// <summary>Records a successful reset run and reschedules the next one.</summary>
    public void MarkSucceeded(DateTimeOffset completedAt, DateTimeOffset nextRunAt)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = "Waiting",
                LastRunCompletedAt = completedAt,
                NextRunAt = nextRunAt,
                LastError = null
            };
        }
    }

    /// <summary>Records a failed reset run and its error message, and reschedules the next attempt.</summary>
    public void MarkFailed(DateTimeOffset failedAt, DateTimeOffset nextRunAt, string error)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = "Failed",
                LastRunCompletedAt = failedAt,
                NextRunAt = nextRunAt,
                LastError = error
            };
        }
    }
}

/// <summary>Immutable point-in-time view of the demo-data reset scheduler's configuration, last run, and outcome.</summary>
public sealed record DemoDataResetSnapshot(
    bool Enabled,
    string Status,
    int IntervalHours,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    string? LastError)
{
    // Default snapshot before the scheduler has run for the first time.
    public static DemoDataResetSnapshot NotStarted() =>
        new(
            Enabled: false,
            Status: "Not started",
            IntervalHours: 0,
            LastRunStartedAt: null,
            LastRunCompletedAt: null,
            NextRunAt: null,
            LastError: null);
}
