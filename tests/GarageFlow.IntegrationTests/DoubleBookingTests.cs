using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// These run against a real SQL Server (the Docker one) because the thing being proved, "two requests at the same
/// moment cannot both win", cannot be proved with an in-memory fake. They are skipped if SQL Server is not running.
/// </summary>
public class DoubleBookingTests
{
    // 05:00 UTC is 10:30 in India: inside opening hours, and in the future.
    private static DateTime Start() => DateTime.UtcNow.Date.AddDays(2).AddHours(5);

    [RequiresSqlServerFact]
    public async Task TenSimultaneousBookings_OfTheSameTechnicianAndBay_OnlyOneWins()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ids = await db.LoadIdsAsync();
        var request = new BookAppointmentRequest(ids.CustomerId, ids.VehicleId, ids.BrakeServiceId, ids.AnilId, ids.Bay1Id, Start());

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            var service = db.NewAppointmentService(out var context);
            await using var _context = context;
            try
            {
                await service.BookAsync(request);
                return "booked";
            }
            catch (ConflictException)
            {
                return "conflict";
            }
        })));

        Assert.Equal(1, outcomes.Count(o => o == "booked"));
        Assert.Equal(9, outcomes.Count(o => o == "conflict"));

        await using var check = db.NewContext();
        Assert.Equal(1, await check.Appointments.CountAsync());
    }

    [RequiresSqlServerFact]
    public async Task TenSimultaneousAutoAssignedBookings_NeverShareATechnicianOrBay()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ids = await db.LoadIdsAsync();
        // General Service can be done by all 3 seeded technicians, and there are 3 bays: exactly 3 can fit.
        var request = new BookAppointmentRequest(ids.CustomerId, ids.VehicleId, ids.GeneralServiceId, null, null, Start());

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            var service = db.NewAppointmentService(out var context);
            await using var _context = context;
            try
            {
                await service.BookAsync(request);
                return "booked";
            }
            catch (ConflictException)
            {
                return "conflict";
            }
        })));

        Assert.Equal(3, outcomes.Count(o => o == "booked"));
        Assert.Equal(7, outcomes.Count(o => o == "conflict"));

        await using var check = db.NewContext();
        var saved = await check.Appointments.ToListAsync();
        Assert.Equal(3, saved.Select(a => a.TechnicianId).Distinct().Count());
        Assert.Equal(3, saved.Select(a => a.BayId).Distinct().Count());
    }

    [RequiresSqlServerFact]
    public async Task TwoPeopleEditingTheSameAppointment_SecondSaveIsRejected()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ids = await db.LoadIdsAsync();
        var service = db.NewAppointmentService(out var serviceContext);
        await using var _serviceContext = serviceContext;
        var booked = await service.BookAsync(
            new BookAppointmentRequest(ids.CustomerId, ids.VehicleId, ids.BrakeServiceId, null, null, Start()));

        // Two screens open the same appointment at the same moment...
        await using var advisorA = db.NewContext();
        await using var advisorB = db.NewContext();
        var copyA = await advisorA.Appointments.SingleAsync(a => a.Id == booked.Id);
        var copyB = await advisorB.Appointments.SingleAsync(a => a.Id == booked.Id);

        // ...A checks the customer in and saves first...
        copyA.CheckIn();
        await advisorA.SaveChangesAsync();

        // ...B, still looking at the old version, tries to cancel. The row version has moved on, so SQL Server refuses.
        copyB.Cancel("Customer called");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => advisorB.SaveChangesAsync());
    }

    [RequiresSqlServerFact]
    public async Task TheDatabaseItself_RefusesTwoActiveAppointmentsStartingTogether()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ids = await db.LoadIdsAsync();
        await using var context = db.NewContext();
        var brakes = await context.ServiceTypes.SingleAsync(s => s.Id == ids.BrakeServiceId);
        var otherTechnician = await context.Technicians.FirstAsync(t => t.Id != ids.AnilId);
        var otherBay = await context.Bays.FirstAsync(b => b.Id != ids.Bay1Id);
        var start = Start();
        var now = DateTime.UtcNow;

        // Written straight to the database, bypassing the booking service and its lock on purpose.
        context.Appointments.Add(Appointment.Book(ids.ShopId, ids.CustomerId, ids.VehicleId, brakes, ids.AnilId, ids.Bay1Id, start, now));
        await context.SaveChangesAsync();

        // Same technician, same start, different bay: refused by UX_Appointments_ActiveTechnicianStart.
        context.Appointments.Add(Appointment.Book(ids.ShopId, ids.CustomerId, ids.VehicleId, brakes, ids.AnilId, otherBay.Id, start, now));
        var technicianClash = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UX_Appointments_ActiveTechnicianStart", technicianClash.GetBaseException().Message);
        context.ChangeTracker.Clear();

        // Same bay, same start, different technician: refused by UX_Appointments_ActiveBayStart.
        context.Appointments.Add(Appointment.Book(ids.ShopId, ids.CustomerId, ids.VehicleId, brakes, otherTechnician.Id, ids.Bay1Id, start, now));
        var bayClash = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.IsType<SqlException>(bayClash.InnerException);
        Assert.Equal(2601, ((SqlException)bayClash.InnerException!).Number);
        context.ChangeTracker.Clear();

        // A CANCELLED appointment does not count, so the same slot can be reused.
        var cancelled = Appointment.Book(ids.ShopId, ids.CustomerId, ids.VehicleId, brakes, ids.AnilId, ids.Bay1Id, start, now);
        cancelled.Cancel("Test");
        context.Appointments.Add(cancelled);
        await context.SaveChangesAsync(); // must not throw
    }
}
