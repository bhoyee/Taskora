using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>
/// <see cref="INotificationEmailSender"/> that delivers notification emails
/// over SMTP using the configured <see cref="SmtpEmailOptions"/>. Registered
/// only when SMTP is enabled; otherwise <see cref="LoggingNotificationEmailSender"/> is used instead.
/// </summary>
public sealed class SmtpNotificationEmailSender(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpNotificationEmailSender> logger)
    : INotificationEmailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Sends the given notification email via SMTP to its distinct recipients, doing nothing if there are none.</summary>
    public async Task SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        Validate(settings);

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };

        // The plain-text Body above always ships as the message's base
        // representation. When an HTML version exists, add it as an
        // AlternateView (not as Body) so the message is built as a proper
        // multipart/alternative MIME message: plain-text-only clients keep
        // showing readable text, and HTML-capable clients render the markup
        // instead of dumping the raw tags as body text.
        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mail.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.HtmlBody,
                    null,
                    "text/html"));
        }

        foreach (var recipient in message.Recipients.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(recipient))
            {
                mail.To.Add(recipient);
            }
        }

        if (mail.To.Count == 0)
        {
            logger.LogInformation(
                "Skipping notification email {Subject} because no recipients were provided.",
                message.Subject);
            return;
        }

        logger.LogInformation(
            "Sending notification email {Subject} to {RecipientCount} recipient(s) through {Host}:{Port}.",
            message.Subject,
            mail.To.Count,
            settings.Host,
            settings.Port);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(settings.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.Username, settings.Password)
        };

        // Callers that fire this from a live HTTP request pass that request's
        // cancellation token in. If the client disconnects mid-request (a
        // closed tab, a flaky connection, a request timeout) that token fires
        // and would otherwise abort an in-flight SMTP send that had nothing
        // to do with the request lifecycle. Use an independent, bounded
        // timeout instead so a dropped client connection can't silently
        // kill a notification email.
        using var sendTimeout = new CancellationTokenSource(SendTimeout);

        try
        {
            await client.SendMailAsync(mail, sendTimeout.Token);
            logger.LogInformation(
                "Notification email {Subject} sent to {RecipientCount} recipient(s).",
                message.Subject,
                mail.To.Count);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Notification email {Subject} failed for {RecipientCount} recipient(s).",
                message.Subject,
                mail.To.Count);
            throw;
        }
    }

    // Fails fast with a clear message when SMTP is enabled but missing
    // required settings, rather than letting SmtpClient throw an opaque error.
    private static void Validate(SmtpEmailOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException(
                "SMTP host is required when Email:Smtp:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException(
                "SMTP from address is required when Email:Smtp:Enabled is true.");
        }
    }
}
