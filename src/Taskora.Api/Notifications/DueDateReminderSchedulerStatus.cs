namespace TodoApp.Api.Notifications;

public sealed class DueDateReminderSchedulerStatus
{
    private readonly object _sync = new();
    private SchedulerSnapshot _snapshot = SchedulerSnapshot.NotStarted();

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
