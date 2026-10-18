using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public sealed class FakeUser(string? email) : ICurrentUser
{
    public Guid? UserId => null;
    public string? Email => email;
}

public class AuditTrailTests
{
    private static DbContextOptions<GarageFlowDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<GarageFlowDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static GarageFlowDbContext As(DbContextOptions<GarageFlowDbContext> options, string? email) =>
        new(options, new FakeUser(email), new FixedClock(Scenario.Now));

    [Fact]
    public async Task CreatingSomething_IsRecordedWithWhoAndWhen()
    {
        var options = NewDatabase();
        await using (var db = As(options, "advisor@garageflow.local"))
        {
            db.Customers.Add(new Customer("Audit Person", "audit@example.com", "9000000009"));
            await db.SaveChangesAsync();
        }

        await using var read = As(options, null);
        var row = Assert.Single(read.AuditLogs.ToList());
        Assert.Equal("Customer", row.EntityName);
        Assert.Equal("Created", row.Action);
        Assert.Equal("advisor@garageflow.local", row.PerformedBy);
        Assert.Equal(Scenario.Now, row.TimestampUtc);
        Assert.Contains("Audit Person", row.Details);
    }

    [Fact]
    public async Task Changes_AreRecordedAsFromTo()
    {
        var options = NewDatabase();
        await using (var db = As(options, "admin@garageflow.local"))
        {
            db.Users.Add(new AppUser("someone@example.com", "Someone", "hash", UserRole.Advisor));
            await db.SaveChangesAsync();
        }

        await using (var db = As(options, "admin@garageflow.local"))
        {
            db.Users.Single().Deactivate();
            await db.SaveChangesAsync();
        }

        await using var read = As(options, null);
        var updated = Assert.Single(read.AuditLogs.Where(a => a.Action == "Updated").ToList());
        Assert.Equal("AppUser", updated.EntityName);
        Assert.Contains("IsActive", updated.Details);
        Assert.Contains("\"from\":true", updated.Details);
        Assert.Contains("\"to\":false", updated.Details);
    }

    [Fact]
    public async Task PasswordHashes_NeverReachTheAuditTrail()
    {
        var options = NewDatabase();
        await using (var db = As(options, "admin@garageflow.local"))
        {
            db.Users.Add(new AppUser("secret@example.com", "Secret", "SUPER-SECRET-HASH-VALUE", UserRole.Admin));
            await db.SaveChangesAsync();
        }

        await using var read = As(options, null);
        var row = Assert.Single(read.AuditLogs.ToList());
        Assert.DoesNotContain("SUPER-SECRET-HASH-VALUE", row.Details);
        Assert.DoesNotContain("PasswordHash", row.Details);
    }

    [Fact]
    public async Task SavingWithoutRealChanges_WritesNoAuditRows()
    {
        var options = NewDatabase();
        await using var db = As(options, "advisor@garageflow.local");
        db.Customers.Add(new Customer("Same Person", "same@example.com", "9000000008"));
        await db.SaveChangesAsync();

        await db.SaveChangesAsync();
        _ = db.Customers.Single(); // read it again, change nothing
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task WithoutALoggedInUser_TheActorIsSystem()
    {
        var options = NewDatabase();
        await using var db = As(options, null);
        db.Customers.Add(new Customer("Job Customer", "job@example.com", "9000000007"));
        await db.SaveChangesAsync();

        Assert.Equal("system", (await db.AuditLogs.SingleAsync()).PerformedBy);
    }

    [Fact]
    public async Task AppointmentHistory_ShowsCreationAndEachStatusChange_WithoutNoise()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await service.CheckInAsync(booked.Id);

        var history = await new AuditService(s.Db).AppointmentHistoryAsync(booked.Id);

        Assert.Equal(new[] { "Created", "Updated" }, history.Select(h => h.Action));
        Assert.Contains("CheckedIn", history[1].Details);
        Assert.DoesNotContain("RowVersion", history[0].Details);
        Assert.All(history, h => Assert.Equal("system", h.PerformedBy));
    }

    [Fact]
    public async Task AppointmentHistory_ForAnUnknownAppointment_IsNotFound()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<NotFoundException>(() => new AuditService(s.Db).AppointmentHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AuditList_CanBeFilteredByPerson_AndEntity_AndIsPaged()
    {
        var options = NewDatabase();
        await using (var a = As(options, "a@example.com"))
        {
            a.Customers.Add(new Customer("First", "first@example.com", "9000000001"));
            a.Customers.Add(new Customer("Second", "second@example.com", "9000000002"));
            await a.SaveChangesAsync();
        }

        await using (var b = As(options, "b@example.com"))
        {
            b.Customers.Add(new Customer("Third", "third@example.com", "9000000003"));
            await b.SaveChangesAsync();
        }

        await using var read = As(options, null);
        var audit = new AuditService(read);

        Assert.Equal(3, (await audit.ListAsync(null, null, null, null)).TotalCount);
        Assert.Equal(2, (await audit.ListAsync(null, null, "A@Example.com", null)).TotalCount); // not case sensitive
        Assert.Equal(0, (await audit.ListAsync("Appointment", null, null, null)).TotalCount);

        var firstPage = await audit.ListAsync("Customer", null, null, new PageRequest(1, 2));
        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNext);
    }
}
