namespace TodoApp.Api.Notifications;

/// <summary>
/// Thread-safe, in-memory holder for the due-date reminder scheduler's
/// latest state, exposed to the Operations dashboard/API. Each mutator
/// swaps the immutable <see cref="SchedulerSnapshot"/> under a lock so
/// readers never observe a partially updated snapshot.
/// </summary>
public sealed class DueDateReminderSchedulerStatus
{
    private readonly object _sync = new();
    private SchedulerSnapshot _snapshot = SchedulerSnapshot.NotStarted();

    /// <summary>The current point-in-time snapshot of the reminder scheduler's status.</summary>
    public SchedulerSnapshot Snapshot
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
                IntervalMinutes = Math.Max(1, (int)Math.Round(interval.TotalMinutes)),
                NextRunAt = nextRunAt,
                Status = enabled ? "Waiting" : "Disabled"
            };
        }
    }

    /// <summary>Records that a reminder run has just started, clearing any prior error.</summary>
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

    /// <summary>Records a successful reminder run's counts (task/project reminders, todo carry-overs, emails sent) and reschedules the next run.</summary>
    public void MarkSucceeded(
        DateTimeOffset completedAt,
        DateTimeOffset nextRunAt,
        int taskReminders,
        int projectReminders,
        int todoCarryOvers,
        int emails)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = "Waiting",
                LastRunCompletedAt = completedAt,
                NextRunAt = nextRunAt,
                LastTaskReminderCount = taskReminders,
                LastProjectReminderCount = projectReminders,
                LastTodoCarryOverCount = todoCarryOvers,
                LastEmailCount = emails,
                LastError = null
            };
        }
    }

    /// <summary>Records a failed reminder run and its error message, and reschedules the next attempt.</summary>
    public void MarkFailed(
        DateTimeOffset failedAt,
        DateTimeOffset nextRunAt,
        string error)
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

/// <summary>Immutable point-in-time view of the due-date reminder scheduler's configuration, last run, and outcome.</summary>
public sealed record SchedulerSnapshot(
    bool Enabled,
    string Status,
    int IntervalMinutes,
    DateTimeOffset? LastRunStartedAt,
    DateTimeOffset? LastRunCompletedAt,
    DateTimeOffset? NextRunAt,
    int LastTaskReminderCount,
    int LastProjectReminderCount,
    int LastTodoCarryOverCount,
    int LastEmailCount,
    string? LastError)
{
    // Default snapshot before the scheduler has run for the first time.
    public static SchedulerSnapshot NotStarted() =>
        new(
            Enabled: false,
            Status: "Not started",
            IntervalMinutes: 0,
            LastRunStartedAt: null,
            LastRunCompletedAt: null,
            NextRunAt: null,
            LastTaskReminderCount: 0,
            LastProjectReminderCount: 0,
            LastTodoCarryOverCount: 0,
            LastEmailCount: 0,
            LastError: null);
}
