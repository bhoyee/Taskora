using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Notifications;

public sealed record SendDueDateNotificationsCommand;

public sealed record DueDateNotificationRunDto(
    int TaskReminderCount,
    int ProjectReminderCount,
    int EmailCount);

public sealed class SendDueDateNotificationsHandler(
    IDueDateNotificationReadRepository notifications,
    INotificationEmailSender emailSender,
    IBusinessDateProvider dates)
{
    public async Task<DueDateNotificationRunDto> HandleAsync(
        SendDueDateNotificationsCommand command,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var taskReminders = await notifications.GetTaskDueNotificationsAsync(
            today,
            cancellationToken);
        var projectReminders =
            await notifications.GetProjectTargetNotificationsAsync(
                today,
                cancellationToken);

        var emailCount = 0;
        foreach (var reminder in taskReminders)
        {
            await emailSender.SendAsync(
                BuildTaskMessage(reminder),
                cancellationToken);
            emailCount++;
        }

        foreach (var reminder in projectReminders)
        {
            await emailSender.SendAsync(
                BuildProjectMessage(reminder),
                cancellationToken);
            emailCount++;
        }

        return new DueDateNotificationRunDto(
            taskReminders.Count,
            projectReminders.Count,
            emailCount);
    }

    private static NotificationEmailMessage BuildTaskMessage(
        TaskDueNotification reminder)
    {
        var timing = reminder.DaysUntilDue switch
        {
            0 => "due today",
            1 => "due in 24 hours",
            2 => "due in 2 days",
            _ => $"due in {reminder.DaysUntilDue} days"
        };

        return TaskoraEmailTemplate.Build(
            reminder.Recipients,
            $"Task reminder: {reminder.TaskTitle} is {timing}",
            "Task reminder",
            $"Task is {timing}",
            "Hello,",
            "This is a professional reminder from Taskora.",
            [
                new EmailDetail("Task", reminder.TaskTitle),
                new EmailDetail("Deadline", reminder.DueDate.ToString("yyyy-MM-dd")),
                new EmailDetail("Status", $"This task is {timing}.")
            ],
            "Please review ownership, update progress, and resolve any blockers before the deadline.");
    }

    private static NotificationEmailMessage BuildProjectMessage(
        ProjectTargetNotification reminder)
    {
        return TaskoraEmailTemplate.Build(
            reminder.Recipients,
            $"Project reminder: {reminder.ProjectName} delivery date is in 24 hours",
            "Project reminder",
            "Project delivery date is in 24 hours",
            "Hello,",
            "This is a professional project delivery-date reminder from Taskora.",
            [
                new EmailDetail("Project", reminder.ProjectName),
                new EmailDetail("Delivery date", reminder.TargetDate.ToString("yyyy-MM-dd")),
                new EmailDetail("Status", "The project delivery date is in 24 hours.")
            ],
            "Please confirm delivery readiness, outstanding tasks, and stakeholder communication.");
    }
}
