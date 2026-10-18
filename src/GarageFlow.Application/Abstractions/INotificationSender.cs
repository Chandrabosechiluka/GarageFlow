namespace GarageFlow.Application.Abstractions;

public record Notification(string ToEmail, string Subject, string Body);

/// <summary>
/// Tells a customer something. Today the implementation only writes to the log; a real e-mail or SMS provider can replace
/// it without touching the jobs, because they only know this interface. Implementations should throw when sending fails,
/// so the job knows NOT to mark the message as sent.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}
