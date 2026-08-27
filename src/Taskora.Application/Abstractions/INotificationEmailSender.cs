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
/// Dispatches a notification email outside the lifetime of the current
/// request or operation, so a slow or hung SMTP round-trip can never delay
/// the caller's response. Used anywhere an email is a side effect of a
/// user-facing action (password reset, invite, task assignment, My Day
/// carry-over on page load) rather than the main point of the operation -
/// scheduled background jobs that already run outside any request (due-date
/// reminders, carry-over notifications) should keep awaiting
/// <see cref="INotificationEmailSender"/> directly instead, since there's no
/// caller response to protect there.
/// </summary>
public interface IBackgroundEmailDispatcher
{
    /// <summary>
    /// Queues the given message to be sent in the background. Returns
    /// immediately; delivery failures are logged, not surfaced to the caller.
    /// </summary>
    void Dispatch(NotificationEmailMessage message);
}

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
