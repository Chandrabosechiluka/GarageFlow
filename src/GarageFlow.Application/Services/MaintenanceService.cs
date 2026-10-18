using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Application.Services;

/// <summary>Settings for the background jobs. Bound from the "Maintenance" section of appsettings; these are the defaults.</summary>
public class MaintenanceSettings
{
    /// <summary>Master switch for the background worker.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the worker wakes up.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>A Booked customer this many minutes after the start time counts as a no-show.</summary>
    public int NoShowGraceMinutes { get; set; } = 30;

    /// <summary>Customers are reminded when their appointment is less than this many hours away.</summary>
    public int ReminderLeadHours { get; set; } = 24;

    /// <summary>Most rows one job handles per run, so a backlog can never hold the database for long.</summary>
    public int BatchSize { get; set; } = 100;
}

public record MaintenanceReport(int NoShowsMarked, int RemindersSent, int OffersNotified, int OffersExpired)
{
    public int Total => NoShowsMarked + RemindersSent + OffersNotified + OffersExpired;
}

public interface IMaintenanceService
{
    /// <summary>Booked appointments whose customer never arrived become NoShow.</summary>
    Task<int> MarkNoShowsAsync(CancellationToken ct = default);

    /// <summary>Tells customers about appointments coming up, once each.</summary>
    Task<int> SendRemindersAsync(CancellationToken ct = default);

    /// <summary>Tells customers about a waitlist offer, once each.</summary>
    Task<int> NotifyOffersAsync(CancellationToken ct = default);

    /// <summary>Offers nobody answered in time expire, and the slot goes to the next customer in line.</summary>
    Task<int> ExpireOffersAsync(CancellationToken ct = default);
}

/// <summary>
/// The work the background worker does on a timer. It lives here, not in the worker, so tests can call it directly with
/// a clock they control. Every job is safe to run twice: it only picks rows that still need work and records the result
/// on the row itself, so a repeat or an overlapping run finds nothing left to do.
/// </summary>
public class MaintenanceService(
    IGarageFlowDbContext db,
    IClock clock,
    INotificationSender notifications,
    MaintenanceSettings settings,
    ILogger<MaintenanceService> logger) : IMaintenanceService
{
    public async Task<int> MarkNoShowsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var grace = TimeSpan.FromMinutes(settings.NoShowGraceMinutes);
        var cutoff = now - grace;

        var candidates = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Booked && a.StartTimeUtc <= cutoff)
            .OrderBy(a => a.StartTimeUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct);

        var marked = 0;
        foreach (var appointment in candidates.Where(a => a.IsNoShowDue(now, grace)))
        {
            appointment.MarkNoShow();
            marked++;
        }

        // One save: either the whole batch is recorded or (on a race) none of it, and the next run retries.
        if (marked > 0 && !await SaveOrStopAsync(ct))
            return 0;
        return marked;
    }

    public async Task<int> SendRemindersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var until = now.AddHours(settings.ReminderLeadHours);

        var due = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Booked
                        && a.ReminderSentUtc == null
                        && a.StartTimeUtc > now
                        && a.StartTimeUtc <= until)
            .OrderBy(a => a.StartTimeUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct);
        if (due.Count == 0)
            return 0;

        var customerIds = due.Select(a => a.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var services = await db.ServiceTypes.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var shops = await db.Shops.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);

        var sent = 0;
        foreach (var appointment in due)
        {
            if (!customers.TryGetValue(appointment.CustomerId, out var customer)
                || !services.TryGetValue(appointment.ServiceTypeId, out var service)
                || !shops.TryGetValue(appointment.ShopId, out var shop))
                continue;

            var local = shop.ToLocalTime(appointment.StartTimeUtc);
            var message = new Notification(
                customer.Email,
                $"Reminder: {service.Name} on {local:ddd d MMM} at {local:HH:mm}",
                $"Hello {customer.FullName}, this is a reminder of your {service.Name} at {shop.Name} on {local:dddd d MMMM} at {local:HH:mm} ({shop.TimeZoneId}). " +
                "If you cannot make it, please cancel or reschedule at least 2 hours before.");

            // Send FIRST, record SECOND. If sending throws, nothing is recorded and the next run tries again
            // (at-least-once delivery: a crash between the two steps can send twice, never zero times).
            await notifications.SendAsync(message, ct);
            appointment.MarkReminderSent(now);
            if (!await SaveOrStopAsync(ct))
                break;
            sent++;
        }

        return sent;
    }

    public async Task<int> NotifyOffersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var open = await db.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Offered && w.OfferNotifiedUtc == null)
            .OrderBy(w => w.CreatedUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct);
        if (open.Count == 0)
            return 0;

        var customerIds = open.Select(w => w.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var services = await db.ServiceTypes.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var shops = await db.Shops.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);

        var sent = 0;
        foreach (var entry in open)
        {
            if (!customers.TryGetValue(entry.CustomerId, out var customer)
                || !services.TryGetValue(entry.ServiceTypeId, out var service)
                || !shops.TryGetValue(entry.ShopId, out var shop)
                || entry.OfferedStartUtc is not { } offeredStart
                || entry.OfferExpiresUtc is not { } expires)
                continue;

            var local = shop.ToLocalTime(offeredStart);
            var answerBy = shop.ToLocalTime(expires);
            var message = new Notification(
                customer.Email,
                $"A slot opened up: {service.Name} on {local:ddd d MMM} at {local:HH:mm}",
                $"Hello {customer.FullName}, a {service.Name} slot is free at {shop.Name} on {local:dddd d MMMM} at {local:HH:mm}. " +
                $"Please accept before {answerBy:HH:mm} ({shop.TimeZoneId}); after that it goes to the next person.");

            await notifications.SendAsync(message, ct);
            entry.MarkOfferNotified(now);
            if (!await SaveOrStopAsync(ct))
                break;
            sent++;
        }

        return sent;
    }

    public async Task<int> ExpireOffersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var lapsed = await db.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Offered && w.OfferExpiresUtc != null && w.OfferExpiresUtc <= now)
            .OrderBy(w => w.OfferExpiresUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var entry in lapsed)
        {
            entry.Expire(now); // the slot details stay on the entry, so they can be handed to the next person in line
            if (!await SaveOrStopAsync(ct))
                break;
            expired++;

            if (entry.OfferedTechnicianId is { } technicianId && entry.OfferedStartUtc is { } start && entry.OfferedEndUtc is { } end)
                await WaitlistOffers.OfferAsync(db, now, entry.ShopId, technicianId, start, end, ct);
        }

        return expired;
    }

    // Two instances (or a request and a job) can touch the same row. The row version makes the loser fail instead of
    // overwrite; for a job that just means "somebody else already did this one", so stop quietly and retry next run.
    private async Task<bool> SaveOrStopAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "A maintenance job lost a race with another change and will retry on its next run.");
            return false;
        }
    }
}
