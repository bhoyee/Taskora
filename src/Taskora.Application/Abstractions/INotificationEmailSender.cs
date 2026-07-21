namespace TodoApp.Application.Abstractions;

public interface INotificationEmailSender
{
    Task SendAsync(
        NotificationEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed record NotificationEmailMessage(
    IReadOnlyCollection<string> Recipients,
    string Subject,
    string Body,
    string? HtmlBody = null);

public interface IApplicationLinkBuilder
{
    string BuildInvitationLink(string token);
}
