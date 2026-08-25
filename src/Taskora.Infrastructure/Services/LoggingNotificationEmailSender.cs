using Microsoft.Extensions.Logging;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>
/// No-op <see cref="INotificationEmailSender"/> that logs the email instead
/// of sending it. Used when SMTP is not configured/enabled (e.g. local
/// development), so notification flows can run end-to-end without a real
/// mail server.
/// </summary>
public sealed class LoggingNotificationEmailSender(
    ILogger<LoggingNotificationEmailSender> logger)
    : INotificationEmailSender
{
    /// <summary>Logs the notification email's recipients, subject, and body instead of sending it.</summary>
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
