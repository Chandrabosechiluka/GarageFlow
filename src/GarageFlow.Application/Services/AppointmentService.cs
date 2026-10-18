using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
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
public class AppointmentService(IGarageFlowDbContext db, IClock clock) : IAppointmentService
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

        var technician = await db.Technicians.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TechnicianId, ct)
            ?? throw new NotFoundException(nameof(Technician), request.TechnicianId);
        if (!technician.HasSkill(service.RequiredSkill))
            throw new DomainException($"{technician.Name} cannot perform {service.Name} (requires {service.RequiredSkill}).");

        var bay = await db.Bays.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BayId, ct)
            ?? throw new NotFoundException(nameof(Bay), request.BayId);
        if (bay.ShopId != technician.ShopId)
            throw new DomainException("The technician and the bay belong to different shops.");

        // The domain checks "UTC" and "in the future", and works out the end time from the service duration.
        var appointment = Appointment.Book(
            technician.ShopId, request.CustomerId, request.VehicleId, service,
            request.TechnicianId, request.BayId, request.StartTimeUtc, clock.UtcNow);

        // Same half-open overlap rule as Appointment.OverlapsWith, written as a query so the database does the work.
        // Cancelled and no-show appointments free the technician and the bay.
        var start = appointment.StartTimeUtc;
        var end = appointment.EndTimeUtc;
        var technicianId = appointment.TechnicianId;
        var bayId = appointment.BayId;

        var clash = await db.Appointments.AnyAsync(a =>
            (a.TechnicianId == technicianId || a.BayId == bayId)
            && a.Status != AppointmentStatus.Cancelled
            && a.Status != AppointmentStatus.NoShow
            && a.StartTimeUtc < end
            && start < a.EndTimeUtc, ct);
        if (clash)
            throw new ConflictException("The technician or the bay is already booked for that time.");

        // NOTE: two requests arriving at the same moment can both pass the check above.
        // Closing that gap (transaction + row version) is the job of Day 6.
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);

        return appointment.ToResponse();
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
