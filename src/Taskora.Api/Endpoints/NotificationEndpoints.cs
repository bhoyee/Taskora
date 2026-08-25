using TodoApp.Application.Notifications;

namespace TodoApp.Api.Endpoints;

/// <summary>
/// Registers the notification-related HTTP endpoints.
/// </summary>
internal static class NotificationEndpoints
{
    /// <summary>
    /// Wires up the notification endpoints on the given route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // POST: manually triggers a due-date reminder notification run (the same
        // work the background scheduler performs automatically). Requires
        // authentication. Returns 200 OK with the handler's result summary.
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
