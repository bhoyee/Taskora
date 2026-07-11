using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

public sealed class SmtpNotificationEmailSender(
    IOptions<SmtpEmailOptions> options)
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
            Body = message.Body,
            IsBodyHtml = false
        };

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
            return;
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(settings.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.Username, settings.Password)
        };

        await client.SendMailAsync(mail, cancellationToken);
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
