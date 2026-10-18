using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IAvailabilityService
{
    Task<AvailabilityResponse> GetAsync(
        Guid serviceTypeId, DateOnly date, Guid? shopId, Guid? technicianId, CancellationToken ct = default);
}

/// <summary>Loads the data, hands it to the pure <see cref="SlotFinder"/>, and shapes the answer for the API.</summary>
public class AvailabilityService(IGarageFlowDbContext db, IClock clock) : IAvailabilityService
{
    public async Task<AvailabilityResponse> GetAsync(
        Guid serviceTypeId, DateOnly date, Guid? shopId, Guid? technicianId, CancellationToken ct = default)
    {
        var service = await db.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceTypeId, ct)
            ?? throw new NotFoundException(nameof(ServiceType), serviceTypeId);

        var shop = await ShopLookup.ResolveAsync(db, shopId, ct);
        var currentShopId = shop.Id;

        var technicians = await db.Technicians.AsNoTracking().Where(t => t.ShopId == currentShopId).ToListAsync(ct);
        if (technicianId.HasValue)
        {
            var wanted = technicians.FirstOrDefault(t => t.Id == technicianId.Value)
                ?? throw new NotFoundException(nameof(Technician), technicianId.Value);
            technicians = new List<Technician> { wanted };
        }

        var bays = await db.Bays.AsNoTracking().Where(b => b.ShopId == currentShopId).ToListAsync(ct);

        // Only appointments that overlap the shop's opening window on that day can block a slot.
        var (open, close) = shop.GetOpenWindowUtc(date);
        var rows = await db.Appointments.AsNoTracking()
            .Where(a => a.ShopId == currentShopId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.NoShow
                        && a.StartTimeUtc < close
                        && open < a.EndTimeUtc)
            .Select(a => new { a.TechnicianId, a.BayId, a.StartTimeUtc, a.EndTimeUtc })
            .ToListAsync(ct);
        var busy = rows.Select(r => new BusyPeriod(r.TechnicianId, r.BayId, r.StartTimeUtc, r.EndTimeUtc)).ToList();

        var slots = SlotFinder.FindSlots(shop, service, technicians, bays, busy, date, clock.UtcNow);

        return new AvailabilityResponse(
            date,
            shop.Id,
            shop.TimeZoneId,
            service.Id,
            service.DurationMinutes,
            slots.Select(s => new SlotResponse(
                s.StartUtc,
                s.EndUtc,
                shop.ToLocalDateTimeOffset(s.StartUtc),
                shop.ToLocalDateTimeOffset(s.EndUtc),
                s.TechnicianIds[0],
                s.BayIds[0],
                s.TechnicianIds,
                s.BayIds)).ToList());
    }
}
