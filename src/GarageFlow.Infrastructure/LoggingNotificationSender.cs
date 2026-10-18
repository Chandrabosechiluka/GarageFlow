using GarageFlow.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Infrastructure;

/// <summary>
/// A stand-in "mail server" that writes the message to the log. It lets the whole reminder flow run and be seen today.
/// A real sender (SMTP, SendGrid, SMS...) would implement the same interface and replace this one in DependencyInjection.
/// </summary>
public sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        logger.LogInformation("NOTIFICATION to {To}: {Subject} | {Body}", notification.ToEmail, notification.Subject, notification.Body);
        return Task.CompletedTask;
    }
}
