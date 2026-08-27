using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TodoApp.Application.Abstractions;

namespace TodoApp.Infrastructure.Services;

/// <summary>
/// <see cref="IBackgroundEmailDispatcher"/> that fires the send on a detached
/// <see cref="Task.Run(Func{Task})"/> with its own DI scope, rather than
/// awaiting it inline. A request-scoped <see cref="INotificationEmailSender"/>
/// (and the DbContext/services it may depend on) is disposed as soon as the
/// HTTP response is sent, so the detached work resolves its own sender from a
/// fresh scope and uses <see cref="CancellationToken.None"/> instead of the
/// caller's token, which would otherwise be cancelled the moment the request
/// ends.
/// </summary>
public sealed class BackgroundEmailDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundEmailDispatcher> logger)
    : IBackgroundEmailDispatcher
{
    /// <summary>Fires the send in the background and returns immediately; failures are logged, not thrown.</summary>
    public void Dispatch(NotificationEmailMessage message)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider
                    .GetRequiredService<INotificationEmailSender>();
                await sender.SendAsync(message, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Background dispatch of notification email {Subject} failed.",
                    message.Subject);
            }
        });
    }
}
