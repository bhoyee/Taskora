namespace TodoApp.Infrastructure.Services;

/// <summary>
/// Configuration for sending notification emails via SMTP. When
/// <see cref="Enabled"/> is false, the DI setup falls back to
/// <see cref="LoggingNotificationEmailSender"/> instead of actually sending mail.
/// </summary>
public sealed class SmtpEmailOptions
{
    /// <summary>When true, notification emails are sent via SMTP; otherwise they are only logged.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Taskora";
}
