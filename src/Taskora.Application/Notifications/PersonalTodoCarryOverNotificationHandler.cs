using TodoApp.Application.Abstractions;
using TodoApp.Application.Todos;

namespace TodoApp.Application.Notifications;

public sealed record SendPersonalTodoCarryOverNotificationsCommand;

public sealed record PersonalTodoCarryOverRunDto(
    int TodoCarryOverCount,
    int UserNotificationCount,
    int EmailCount);

public sealed class SendPersonalTodoCarryOverNotificationsHandler(
    IPersonalTodoRepository todos,
    GenerateDailyRoutineTodosHandler dailyRoutines,
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
        await dailyRoutines.HandleAsync(
            new GenerateDailyRoutineTodosCommand(today),
            cancellationToken);
        var overdueTodos = await todos.ListIncompleteBeforeAsync(
            today,
            cancellationToken);
        if (overdueTodos.Count == 0)
        {
            return new PersonalTodoCarryOverRunDto(0, 0, 0);
        }

        var carryOvers = overdueTodos
            .Select(todo => new PersonalTodoCarryOverCandidate(
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
                PersonalTodoCarryOverEmailFactory.Build(
                    owner,
                    group
                        .Select(item => new PersonalTodoCarryOverEmailItem(
                            item.Title,
                            item.FromDate))
                        .ToArray(),
                    today),
                cancellationToken);
            notifiedUsers++;
            emailCount++;
        }

        return new PersonalTodoCarryOverRunDto(
            carryOvers.Length,
            notifiedUsers,
            emailCount);
    }

    private sealed record PersonalTodoCarryOverCandidate(
        Guid UserId,
        string Title,
        DateOnly FromDate,
        DateOnly ToDate);
}

public sealed record PersonalTodoCarryOverEmailItem(
    string Title,
    DateOnly FromDate);

public static class PersonalTodoCarryOverEmailFactory
{
    public static NotificationEmailMessage Build(
        PersonalTodoOwner owner,
        IReadOnlyCollection<PersonalTodoCarryOverEmailItem> items,
        DateOnly today)
    {
        var carriedItems = string.Join(
            "; ",
            items
            .OrderBy(item => item.FromDate)
            .ThenBy(item => item.Title)
            .Take(10)
            .Select(item => $"{item.Title} from {item.FromDate:yyyy-MM-dd}"));
        var remaining = items.Count > 10
            ? $" Plus {items.Count - 10} more."
            : string.Empty;

        return TaskoraEmailTemplate.Build(
            [owner.Email],
            $"My Day carryover: {items.Count} todo{(items.Count == 1 ? string.Empty : "s")} moved to today",
            "My Day carryover",
            "Incomplete todos were moved into today",
            $"Hello {owner.DisplayName},",
            "Taskora carried over unfinished My Day todos so they stay visible instead of being left behind.",
            [
                new EmailDetail("New date", today.ToString("yyyy-MM-dd")),
                new EmailDetail("Carryover count", items.Count.ToString()),
                new EmailDetail("Carried-over todos", carriedItems + remaining)
            ],
            "Open My Day in Taskora to review, complete, or reschedule these todos.");
    }
}
