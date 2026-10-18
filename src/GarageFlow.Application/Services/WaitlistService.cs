using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IWaitlistService
{
    Task<WaitlistEntryResponse> JoinAsync(JoinWaitlistRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistEntryResponse>> ListAsync(WaitlistStatus? status, CancellationToken ct = default);
    Task<WaitlistEntryResponse> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>Turns an open offer into a real appointment (assigning a technician and bay) and returns it.</summary>
    Task<AppointmentResponse> AcceptOfferAsync(Guid id, CancellationToken ct = default);
}

public class WaitlistService(IGarageFlowDbContext db, IClock clock, IAppointmentService appointments) : IWaitlistService
{
    public async Task<WaitlistEntryResponse> JoinAsync(JoinWaitlistRequest request, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw new NotFoundException(nameof(Vehicle), request.VehicleId);
        if (vehicle.CustomerId != request.CustomerId)
            throw new DomainException("This vehicle does not belong to the customer.");

        if (!await db.ServiceTypes.AnyAsync(s => s.Id == request.ServiceTypeId, ct))
            throw new NotFoundException(nameof(ServiceType), request.ServiceTypeId);

        var shop = await ShopLookup.ResolveAsync(db, request.ShopId, ct);

        // One open request per vehicle and service is enough; two would just compete with each other.
        var open = new[] { WaitlistStatus.Waiting, WaitlistStatus.Offered, WaitlistStatus.Accepting };
        if (await db.WaitlistEntries.AnyAsync(w => w.VehicleId == request.VehicleId
                                                   && w.ServiceTypeId == request.ServiceTypeId
                                                   && open.Contains(w.Status), ct))
            throw new ConflictException("This vehicle is already on the waitlist for that service.");

        var entry = WaitlistEntry.Join(
            shop.Id, request.CustomerId, request.VehicleId, request.ServiceTypeId,
            request.EarliestStartUtc, request.LatestStartUtc, clock.UtcNow);

        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.ToResponse();
    }

    public async Task<IReadOnlyList<WaitlistEntryResponse>> ListAsync(WaitlistStatus? status, CancellationToken ct = default)
    {
        IQueryable<WaitlistEntry> query = db.WaitlistEntries.AsNoTracking();
        if (status.HasValue)
        {
            var wanted = status.Value;
            query = query.Where(w => w.Status == wanted);
        }

        var entries = await query.OrderBy(w => w.CreatedUtc).Take(200).ToListAsync(ct);
        return entries.Select(w => w.ToResponse()).ToList();
    }

    public async Task<WaitlistEntryResponse> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await LoadAsync(id, ct);
        entry.Cancel();
        await db.SaveChangesAsync(ct);
        return entry.ToResponse();
    }

    public async Task<AppointmentResponse> AcceptOfferAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await LoadAsync(id, ct);

        // Step 1: claim the offer and save at once. Two people pressing "accept" together both read "Offered",
        // but the row version lets only ONE save succeed; the other gets a 409 before anything is booked.
        var start = entry.BeginAccept();
        await db.SaveChangesAsync(ct);

        try
        {
            // Step 2: a normal booking, through the same lock and the same rules as everyone else.
            var appointment = await appointments.BookAsync(
                new BookAppointmentRequest(entry.CustomerId, entry.VehicleId, entry.ServiceTypeId, null, null, start), ct);

            // Step 3: record the result.
            entry.Fulfil(appointment.Id);
            await db.SaveChangesAsync(ct);
            return appointment;
        }
        catch (Exception ex) when (ex is ConflictException or DomainException or NotFoundException)
        {
            // The slot was taken (or became invalid) in the meantime: the customer goes back to waiting.
            entry.WithdrawOffer();
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<WaitlistEntry> LoadAsync(Guid id, CancellationToken ct) =>
        await db.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new NotFoundException(nameof(WaitlistEntry), id);
}
