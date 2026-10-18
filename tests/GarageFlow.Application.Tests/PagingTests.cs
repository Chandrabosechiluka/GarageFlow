using GarageFlow.Application.Common;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void PageRequest_RejectsNonsense(int page, int pageSize)
    {
        Assert.Throws<DomainException>(() => new PageRequest(page, pageSize).Validated());
    }

    [Fact]
    public void PageRequest_AcceptsTheLimits()
    {
        Assert.Equal(0, new PageRequest(1, 1).Validated().Skip);
        Assert.Equal(100, new PageRequest(2, 100).Validated().Skip);
    }

    [Fact]
    public void PagedResult_ComputesPagesAndNeighbours()
    {
        var first = new PagedResult<int>(new[] { 1 }, 1, 20, 45);
        var last = new PagedResult<int>(new[] { 1 }, 3, 20, 45);
        var empty = new PagedResult<int>(Array.Empty<int>(), 1, 20, 0);

        Assert.Equal(3, first.TotalPages);
        Assert.True(first.HasNext);
        Assert.False(first.HasPrevious);
        Assert.False(last.HasNext);
        Assert.True(last.HasPrevious);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNext);
    }

    [Fact]
    public async Task Appointments_ArePagedWithoutLosingOrRepeatingAnyone_EvenWhenStartTimesTie()
    {
        var s = new Scenario();
        var service = s.Appointments();
        // Five appointments; two pairs start at exactly the same moment, which is where unstable sorting goes wrong.
        var starts = new[] { 0, 1, 2 };
        foreach (var hour in starts)
            await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm.AddHours(hour)));
        foreach (var hour in new[] { 0, 1 })
            await service.BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm.AddHours(hour)));

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await service.ListAsync(null, null, null, new PageRequest(page, 2));
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
            seen.AddRange(result.Items.Select(a => a.Id));
        }

        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task Appointments_AreSortedByStartTime_AndAPastTheEndPageIsEmpty()
    {
        var s = new Scenario();
        var service = s.Appointments();
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm.AddHours(2)));
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var first = await service.ListAsync(null, null, null, new PageRequest(1, 10));
        Assert.Equal(new[] { Scenario.NineAm, Scenario.NineAm.AddHours(2) }, first.Items.Select(a => a.StartTimeUtc));

        var beyond = await service.ListAsync(null, null, null, new PageRequest(5, 10));
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.TotalCount);
    }

    [Fact]
    public async Task Appointments_TheDefaultPageIsUsedWhenNoneIsGiven()
    {
        var s = new Scenario();
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var result = await s.Appointments().ListAsync(null, null, AppointmentStatus.Booked);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Appointments_RejectAnInvalidPage()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<DomainException>(() => s.Appointments().ListAsync(null, null, null, new PageRequest(0, 20)));
    }

    [Fact]
    public async Task Customers_ArePagedAlphabetically()
    {
        var s = new Scenario();
        s.Db.Customers.Add(new Customer("Alpha Person", "alpha@example.com", "9000000002"));
        s.Db.Customers.Add(new Customer("Zed Person", "zed@example.com", "9000000003"));
        await s.Db.SaveChangesAsync();
        var service = new CustomerService(s.Db);

        var first = await service.ListAsync(null, new PageRequest(1, 2));
        var second = await service.ListAsync(null, new PageRequest(2, 2));

        Assert.Equal(new[] { "Alpha Person", "Test Customer" }, first.Items.Select(c => c.FullName));
        Assert.Equal(new[] { "Zed Person" }, second.Items.Select(c => c.FullName));
        Assert.Equal(3, first.TotalCount);
    }
}
