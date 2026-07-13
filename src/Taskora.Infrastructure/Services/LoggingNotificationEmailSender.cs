using Microsoft.Extensions.Logging;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class LoggingNotificationEmailSender(
    ILogger<LoggingNotificationEmailSender> logger)
    : INotificationEmailSender
{
    public Task SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Notification email queued for {Recipients}. Subject: {Subject}. Body: {Body}",
            string.Join(", ", message.Recipients),
            message.Subject,
            message.Body);
        return Task.CompletedTask;
    }
}
