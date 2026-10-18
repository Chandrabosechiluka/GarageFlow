using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IAppointmentService
{
    Task<AppointmentResponse> BookAsync(BookAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentResponse>> ListAsync(DateTime? fromUtc, DateTime? toUtc, AppointmentStatus? status, CancellationToken ct = default);
    Task<AppointmentResponse> CheckInAsync(Guid id, CancellationToken ct = default);
    Task<AppointmentResponse> StartInspectionAsync(Guid id, CancellationToken ct = default);
    Task<AppointmentResponse> AddInspectionItemAsync(Guid id, AddInspectionItemRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> CompleteAsync(Guid id, CancellationToken ct = default);
    Task<AppointmentResponse> CancelAsync(Guid id, CancelAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> MarkNoShowAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates a use case: load the data, ask the domain objects to enforce the rules, save.
/// The business rules themselves (state machine, UTC, cancellation reason) stay in the Domain project.
/// </summary>
public class AppointmentService(IGarageFlowDbContext db, IClock clock, IBookingGate gate) : IAppointmentService
{
    public async Task<AppointmentResponse> BookAsync(BookAppointmentRequest request, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);
        if (vehicle.CustomerId != request.CustomerId)
            throw new DomainException("This vehicle does not belong to the customer.");

        var service = await db.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceTypeId, ct)
            ?? throw new NotFoundException(nameof(ServiceType), request.ServiceTypeId);

        // A technician or bay is optional. When given it is checked; when empty one is assigned below.
        Technician? requestedTechnician = null;
        if (request.TechnicianId is { } requestedTechnicianId)
        {
            requestedTechnician = await db.Technicians.AsNoTracking().FirstOrDefaultAsync(t => t.Id == requestedTechnicianId, ct)
                ?? throw new NotFoundException(nameof(Technician), requestedTechnicianId);
            if (!requestedTechnician.HasSkill(service.RequiredSkill))
                throw new DomainException($"{requestedTechnician.Name} cannot perform {service.Name} (requires {service.RequiredSkill}).");
        }

        Bay? requestedBay = null;
        if (request.BayId is { } requestedBayId)
        {
            requestedBay = await db.Bays.AsNoTracking().FirstOrDefaultAsync(b => b.Id == requestedBayId, ct)
                ?? throw new NotFoundException(nameof(Bay), requestedBayId);
        }

        if (requestedTechnician is not null && requestedBay is not null && requestedBay.ShopId != requestedTechnician.ShopId)
            throw new DomainException("The technician and the bay belong to different shops.");

        var shop = await ShopLookup.ResolveAsync(db, requestedTechnician?.ShopId ?? requestedBay?.ShopId, ct);
        var shopId = shop.Id;

        // Same start-time rules as Appointment.Book, checked early so the caller gets the right message first.
        Appointment.ValidateStart(request.StartTimeUtc, clock.UtcNow);

        var start = request.StartTimeUtc;
        var end = start.AddMinutes(service.DurationMinutes);

        if (!shop.IsOpenForUtc(start, end))
            throw new DomainException(
                $"The shop is closed at that time. Opening hours are {shop.OpeningTime.ToString("HH:mm")}-{shop.ClosingTime.ToString("HH:mm")} ({shop.TimeZoneId}).");

        // CRITICAL SECTION. "Is it free?" and "take it" must be one indivisible step, otherwise two requests arriving
        // together both see "free" and both book it. The gate lets one booking per shop run at a time and wraps the
        // reads and the insert in one transaction. The cheap validation above stays outside so it never holds the lock.
        return await gate.RunExclusiveAsync($"booking:{shopId}", async token =>
        {
            // Everything active in this shop that overlaps the wanted time: who and what is already taken.
            var rows = await db.Appointments.AsNoTracking()
                .Where(a => a.ShopId == shopId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.NoShow
                            && a.StartTimeUtc < end
                            && start < a.EndTimeUtc)
                .Select(a => new { a.TechnicianId, a.BayId, a.StartTimeUtc, a.EndTimeUtc })
                .ToListAsync(token);
            var busy = rows.Select(r => new BusyPeriod(r.TechnicianId, r.BayId, r.StartTimeUtc, r.EndTimeUtc)).ToList();

            Guid technicianId;
            if (requestedTechnician is not null)
            {
                if (busy.Any(b => b.TechnicianId == requestedTechnician.Id && b.Overlaps(start, end)))
                    throw new ConflictException("That technician is already booked for that time.");
                technicianId = requestedTechnician.Id;
            }
            else
            {
                var shopTechnicians = await db.Technicians.AsNoTracking().Where(t => t.ShopId == shopId).ToListAsync(token);
                var freeTechnicians = SlotFinder.FreeTechnicians(
                    shopTechnicians.Where(t => t.HasSkill(service.RequiredSkill)), busy, start, end);
                if (freeTechnicians.Count == 0)
                    throw new ConflictException("No qualified technician is free at that time.");
                technicianId = freeTechnicians[0];
            }

            Guid bayId;
            if (requestedBay is not null)
            {
                if (busy.Any(b => b.BayId == requestedBay.Id && b.Overlaps(start, end)))
                    throw new ConflictException("That bay is already booked for that time.");
                bayId = requestedBay.Id;
            }
            else
            {
                var shopBays = await db.Bays.AsNoTracking().Where(b => b.ShopId == shopId).ToListAsync(token);
                var freeBays = SlotFinder.FreeBays(shopBays, busy, start, end);
                if (freeBays.Count == 0)
                    throw new ConflictException("No bay is free at that time.");
                bayId = freeBays[0];
            }

            var appointment = Appointment.Book(
                shopId, request.CustomerId, request.VehicleId, service, technicianId, bayId, start, clock.UtcNow);

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync(token);

            return appointment.ToResponse();
        }, ct);
    }

    public async Task<AppointmentResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(a => a.InspectionItems)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Appointment), id);

        return appointment.ToResponse();
    }

    public async Task<IReadOnlyList<AppointmentResponse>> ListAsync(
        DateTime? fromUtc, DateTime? toUtc, AppointmentStatus? status, CancellationToken ct = default)
    {
        IQueryable<Appointment> query = db.Appointments.AsNoTracking().Include(a => a.InspectionItems);

        // Appointments that overlap the requested window.
        if (fromUtc.HasValue)
        {
            var from = ToUtc(fromUtc.Value);
            query = query.Where(a => a.EndTimeUtc > from);
        }

        if (toUtc.HasValue)
        {
            var to = ToUtc(toUtc.Value);
            query = query.Where(a => a.StartTimeUtc < to);
        }

        if (status.HasValue)
        {
            var wanted = status.Value;
            query = query.Where(a => a.Status == wanted);
        }

        // Real pagination arrives on Day 9; until then the list is capped.
        var appointments = await query.OrderBy(a => a.StartTimeUtc).Take(200).ToListAsync(ct);
        return appointments.Select(a => a.ToResponse()).ToList();
    }

    public Task<AppointmentResponse> CheckInAsync(Guid id, CancellationToken ct = default) =>
        ChangeAsync(id, a => a.CheckIn(), ct);

    public Task<AppointmentResponse> StartInspectionAsync(Guid id, CancellationToken ct = default) =>
        ChangeAsync(id, a => a.StartInspection(), ct);

    public Task<AppointmentResponse> CompleteAsync(Guid id, CancellationToken ct = default) =>
        ChangeAsync(id, a => a.Complete(), ct);

    public Task<AppointmentResponse> CancelAsync(Guid id, CancelAppointmentRequest request, CancellationToken ct = default) =>
        ChangeAsync(id, a => a.Cancel(request.Reason), ct);

    public Task<AppointmentResponse> MarkNoShowAsync(Guid id, CancellationToken ct = default) =>
        ChangeAsync(id, a => a.MarkNoShow(), ct);

    public async Task<AppointmentResponse> AddInspectionItemAsync(Guid id, AddInspectionItemRequest request, CancellationToken ct = default)
    {
        var appointment = await LoadAsync(id, ct);

        var item = appointment.AddInspectionItem(request.Name, request.Condition, request.Notes);
        db.InspectionItems.Add(item);
        await db.SaveChangesAsync(ct);

        return appointment.ToResponse();
    }

    // Shared shape of every status change: load, let the domain apply the rule, save.
    private async Task<AppointmentResponse> ChangeAsync(Guid id, Action<Appointment> change, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        change(appointment);
        await db.SaveChangesAsync(ct);
        return appointment.ToResponse();
    }

    private async Task<Appointment> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Appointments.Include(a => a.InspectionItems).FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(nameof(Appointment), id);

    // Query-string dates such as "...Z" arrive as Local; a date with no zone is treated as UTC. Either way compare in UTC.
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
