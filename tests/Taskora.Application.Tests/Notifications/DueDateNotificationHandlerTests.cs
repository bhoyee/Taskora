using TodoApp.Application.Abstractions;
using TodoApp.Application.Notifications;

namespace TodoApp.Application.Tests.Notifications;

// Covers SendDueDateNotificationsHandler, which emails reminders for tasks
// and projects approaching their due/target dates.
public sealed class DueDateNotificationHandlerTests
{
    [Fact]
    public async Task Handle_SendsTaskAndProjectReminderEmails()
    {
        var reader = new StubDueDateNotificationReader(
            [
                new TaskDueNotification(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Ship portfolio",
                    new DateOnly(2026, 7, 11),
                    1,
                    ["owner@example.com", "member@example.com"])
            ],
            [
                new ProjectTargetNotification(
                    Guid.NewGuid(),
                    "Portfolio launch",
                    new DateOnly(2026, 7, 11),
                    1,
                    ["owner@example.com"])
            ]);
        var sender = new RecordingEmailSender();
        var handler = new SendDueDateNotificationsHandler(
            reader,
            sender,
            new StubBusinessDateProvider(new DateOnly(2026, 7, 10)));

        var result = await handler.HandleAsync(
            new SendDueDateNotificationsCommand(),
            CancellationToken.None);

        Assert.Equal(1, result.TaskReminderCount);
        Assert.Equal(1, result.ProjectReminderCount);
        Assert.Equal(2, result.EmailCount);
        Assert.Contains(
            sender.Messages,
            message => message.Subject.Contains(
                "Ship portfolio is due in 24 hours",
                StringComparison.Ordinal));
        Assert.Contains(
            sender.Messages,
            message => message.Body.Contains(
                "confirm delivery readiness",
                StringComparison.OrdinalIgnoreCase));
    }

    // IDueDateNotificationReadRepository stub returning fixed task and
    // project reminder lists regardless of the requested date.
    private sealed class StubDueDateNotificationReader(
        IReadOnlyList<TaskDueNotification> taskReminders,
        IReadOnlyList<ProjectTargetNotification> projectReminders)
        : IDueDateNotificationReadRepository
    {
        public Task<IReadOnlyList<TaskDueNotification>> GetTaskDueNotificationsAsync(
            DateOnly today,
            CancellationToken cancellationToken) =>
            Task.FromResult(taskReminders);

        public Task<IReadOnlyList<ProjectTargetNotification>> GetProjectTargetNotificationsAsync(
            DateOnly today,
            CancellationToken cancellationToken) =>
            Task.FromResult(projectReminders);
    }

    // INotificationEmailSender fake that records every message sent.
    private sealed class RecordingEmailSender : INotificationEmailSender
    {
        public List<NotificationEmailMessage> Messages { get; } = [];

        public Task SendAsync(
            NotificationEmailMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    // IBusinessDateProvider stub returning a fixed "today" business date.
    private sealed class StubBusinessDateProvider(DateOnly today)
        : IBusinessDateProvider
    {
        public DateOnly Today { get; } = today;

        public string TimeZoneId => "Europe/London";
    }
}
