using TodoApp.Application.Notifications;

namespace TodoApp.Api.Endpoints;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/notifications/due-date-reminders/run",
                async (
                    SendDueDateNotificationsHandler handler,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await handler.HandleAsync(
                        new SendDueDateNotificationsCommand(),
                        cancellationToken)))
            .WithTags("Notifications")
            .RequireAuthorization()
            .WithName("RunDueDateReminderNotifications");

        return endpoints;
    }
}
