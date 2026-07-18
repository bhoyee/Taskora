using System.Text;
using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Notifications;

public sealed record SendPersonalTodoCarryOverNotificationsCommand;

public sealed record PersonalTodoCarryOverRunDto(
    int TodoCarryOverCount,
    int UserNotificationCount,
    int EmailCount);

public sealed class SendPersonalTodoCarryOverNotificationsHandler(
    IPersonalTodoRepository todos,
    INotificationEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IClock clock,
    IBusinessDateProvider dates)
{
    public async Task<PersonalTodoCarryOverRunDto> HandleAsync(
        SendPersonalTodoCarryOverNotificationsCommand command,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var overdueTodos = await todos.ListIncompleteBeforeAsync(
            today,
            cancellationToken);
        if (overdueTodos.Count == 0)
        {
            return new PersonalTodoCarryOverRunDto(0, 0, 0);
        }

        var carryOvers = overdueTodos
            .Select(todo => new PersonalTodoCarryOverItem(
                todo.UserId,
                todo.Title,
                todo.TodoDate,
                today))
            .ToArray();

        foreach (var todo in overdueTodos)
        {
            todo.CarryOverTo(today, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var owners = await todos.ListOwnersAsync(
                carryOvers.Select(todo => todo.UserId).Distinct().ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var ownersByUserId = owners.ToDictionary(owner => owner.UserId);

        var emailCount = 0;
        var notifiedUsers = 0;
        foreach (var group in carryOvers.GroupBy(todo => todo.UserId))
        {
            if (!ownersByUserId.TryGetValue(group.Key, out var owner) ||
                string.IsNullOrWhiteSpace(owner.Email))
            {
                continue;
            }

            await emailSender.SendAsync(
                BuildCarryOverMessage(owner, group.ToArray(), today),
                cancellationToken);
            notifiedUsers++;
            emailCount++;
        }

        return new PersonalTodoCarryOverRunDto(
            carryOvers.Length,
            notifiedUsers,
            emailCount);
    }

    private static NotificationEmailMessage BuildCarryOverMessage(
        PersonalTodoOwner owner,
        IReadOnlyCollection<PersonalTodoCarryOverItem> items,
        DateOnly today)
    {
        var body = new StringBuilder();
        body.AppendLine($"Hello {owner.DisplayName},");
        body.AppendLine();
        body.AppendLine(
            "Taskora carried over incomplete My Day todos into today so they stay visible instead of being left behind.");
        body.AppendLine();
        body.AppendLine($"New date: {today:yyyy-MM-dd}");
        body.AppendLine($"Carryover count: {items.Count}");
        body.AppendLine();
        body.AppendLine("Carried-over todos:");

        foreach (var item in items
            .OrderBy(item => item.FromDate)
            .ThenBy(item => item.Title)
            .Take(10))
        {
            body.AppendLine(
                $"- {item.Title} (from {item.FromDate:yyyy-MM-dd})");
        }

        if (items.Count > 10)
        {
            body.AppendLine($"- Plus {items.Count - 10} more.");
        }

        body.AppendLine();
        body.AppendLine(
            "Please review My Day, complete what is done, or reschedule anything that no longer belongs today.");

        return new NotificationEmailMessage(
            [owner.Email],
            $"My Day carryover: {items.Count} todo{(items.Count == 1 ? string.Empty : "s")} moved to today",
            body.ToString());
    }

    private sealed record PersonalTodoCarryOverItem(
        Guid UserId,
        string Title,
        DateOnly FromDate,
        DateOnly ToDate);
}
