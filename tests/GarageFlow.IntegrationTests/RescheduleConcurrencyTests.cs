using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// Moving an appointment is "check it is free, then take it", exactly like booking. These prove it uses the same lock.
/// </summary>
public class RescheduleConcurrencyTests
{
    private static DateTime Start() => DateTime.UtcNow.Date.AddDays(2).AddHours(5);

    [RequiresSqlServerFact]
    public async Task TwoAppointments_MovedIntoTheSameFreeSlotTogether_OnlyOneWins()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ids = await db.LoadIdsAsync();

        // Two appointments for the same technician and bay at different times, plus a free hour between them.
        var service = db.NewAppointmentService(out var setup);
        await using var _setup = setup;
        var first = await service.BookAsync(new BookAppointmentRequest(ids.CustomerId, ids.VehicleId, ids.BrakeServiceId, ids.AnilId, ids.Bay1Id, Start().AddHours(3)));
        var second = await service.BookAsync(new BookAppointmentRequest(ids.CustomerId, ids.VehicleId, ids.BrakeServiceId, ids.AnilId, ids.Bay1Id, Start().AddHours(5)));

        var target = new RescheduleAppointmentRequest(Start().AddHours(1), ids.AnilId, ids.Bay1Id);

        var outcomes = await Task.WhenAll(new[] { first.Id, second.Id }.Select(id => Task.Run(async () =>
        {
            var worker = db.NewAppointmentService(out var context);
            await using var _context = context;
            try
            {
                await worker.RescheduleAsync(id, target);
                return "moved";
            }
            catch (ConflictException)
            {
                return "conflict";
            }
        })));

        Assert.Equal(1, outcomes.Count(o => o == "moved"));
        Assert.Equal(1, outcomes.Count(o => o == "conflict"));

        await using var check = db.NewContext();
        var starts = await check.Appointments.Select(a => a.StartTimeUtc).ToListAsync();
        Assert.Equal(starts.Count, starts.Distinct().Count());
    }
}
