using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class SmtpNotificationEmailSender(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpNotificationEmailSender> logger)
    : INotificationEmailSender
{
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
            Body = string.IsNullOrWhiteSpace(message.HtmlBody)
                ? message.Body
                : message.HtmlBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mail.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.Body,
                    null,
                    "text/plain"));
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

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
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
