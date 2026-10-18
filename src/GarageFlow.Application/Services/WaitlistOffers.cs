using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

/// <summary>
/// "A slot is free: who on the waitlist gets it?" Used when an appointment is cancelled or moved, and again by the
/// background job when an offer expires and the slot has to be passed to the next customer.
/// </summary>
internal static class WaitlistOffers
{
    /// <summary>
    /// Offers the freed time (this technician, from start to end) to the oldest waiting customer whose window contains
    /// the start, whose service fits and can be done by this technician. Returns true when someone was offered it.
    /// It is a courtesy step after the real change succeeded, so it never throws for a lost race.
    /// </summary>
    public static async Task<bool> OfferAsync(
        IGarageFlowDbContext db, DateTime nowUtc, Guid shopId, Guid technicianId, DateTime start, DateTime end, CancellationToken ct)
    {
        if (start <= nowUtc)
            return false;

        var technician = await db.Technicians.AsNoTracking().FirstOrDefaultAsync(t => t.Id == technicianId, ct);
        if (technician is null)
            return false;

        // Still free? Right after a cancel it always is, but after an expired offer someone may have booked it meanwhile.
        var active = await db.Appointments.AsNoTracking()
            .Where(a => a.ShopId == shopId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.NoShow
                        && a.StartTimeUtc < end
                        && start < a.EndTimeUtc)
            .Select(a => new { a.TechnicianId, a.BayId })
            .ToListAsync(ct);
        if (active.Any(a => a.TechnicianId == technicianId))
            return false;
        var bayCount = await db.Bays.CountAsync(b => b.ShopId == shopId, ct);
        if (active.Select(a => a.BayId).Distinct().Count() >= bayCount)
            return false;

        // The same slot must not be offered to two customers at once.
        var alreadyOffered = await db.WaitlistEntries.AnyAsync(w =>
            w.Status == WaitlistStatus.Offered && w.OfferedTechnicianId == technicianId && w.OfferedStartUtc == start, ct);
        if (alreadyOffered)
            return false;

        var freedMinutes = (end - start).TotalMinutes;
        var services = await db.ServiceTypes.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);

        var candidates = await db.WaitlistEntries
            .Where(w => w.ShopId == shopId
                        && w.Status == WaitlistStatus.Waiting
                        && w.EarliestStartUtc <= start
                        && start <= w.LatestStartUtc)
            .OrderBy(w => w.CreatedUtc)
            .Take(50)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(w =>
            services.TryGetValue(w.ServiceTypeId, out var service)
            && service.DurationMinutes <= freedMinutes
            && technician.HasSkill(service.RequiredSkill));
        if (match is null)
            return false;

        try
        {
            match.Offer(start, end, technicianId, nowUtc);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false; // another request touched the same entry first; the main action already succeeded
        }
    }
}
