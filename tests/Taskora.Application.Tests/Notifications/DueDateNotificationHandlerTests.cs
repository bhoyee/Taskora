using TodoApp.Application.Abstractions;
using TodoApp.Application.Notifications;

namespace TodoApp.Application.Tests.Notifications;

public sealed class DueDateNotificationHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 10, 9, 0, 0, TimeSpan.Zero);

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
            new StubClock(Now));

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

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
