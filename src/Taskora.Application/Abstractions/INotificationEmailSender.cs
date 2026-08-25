namespace TodoApp.Application.Abstractions;

/// <summary>
/// Abstraction for sending notification emails, decoupling the application layer
/// from any particular email delivery provider.
/// </summary>
public interface INotificationEmailSender
{
    /// <summary>
    /// Sends the given notification email message.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed record NotificationEmailMessage(
    IReadOnlyCollection<string> Recipients,
    string Subject,
    string Body,
    string? HtmlBody = null);

/// <summary>
/// Abstraction for building absolute application URLs for links embedded in emails.
/// </summary>
public interface IApplicationLinkBuilder
{
    /// <summary>
    /// Builds the absolute URL a user follows to accept a workspace invitation.
    /// </summary>
    /// <param name="token">The invitation token to embed in the link.</param>
    /// <returns>The absolute invitation link URL.</returns>
    string BuildInvitationLink(string token);
}
