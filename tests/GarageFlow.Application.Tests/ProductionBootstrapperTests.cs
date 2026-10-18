using GarageFlow.Domain.Enums;
using GarageFlow.Infrastructure.Auth;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public class ProductionBootstrapperTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new(iterations: 1_000); // fast on purpose: this is a test

    private static GarageFlowDbContext EmptyDb() =>
        new(new DbContextOptionsBuilder<GarageFlowDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static BootstrapOptions Valid() => new()
    {
        AdminEmail = "Owner@Garage.Example",
        AdminPassword = "a-long-first-password",
        AdminDisplayName = "Owner",
        ShopName = "Hyderabad Motors",
        OpeningTime = "08:30",
        ClosingTime = "19:00",
        TimeZoneId = "Asia/Kolkata"
    };

    [Fact]
    public async Task OnAnEmptyDatabase_CreatesTheShopTheStandardServicesAndTheAdmin()
    {
        await using var db = EmptyDb();

        var result = await ProductionBootstrapper.RunAsync(db, Hasher, Valid());

        Assert.True(result.ShopCreated);
        Assert.True(result.AdminCreated);

        var shop = await db.Shops.SingleAsync();
        Assert.Equal("Hyderabad Motors", shop.Name);
        Assert.Equal(new TimeOnly(8, 30), shop.OpeningTime);
        Assert.Equal(new TimeOnly(19, 0), shop.ClosingTime);
        Assert.Equal(5, await db.ServiceTypes.CountAsync());

        var admin = await db.Users.SingleAsync();
        Assert.Equal("owner@garage.example", admin.Email);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(Hasher.Verify("a-long-first-password", admin.PasswordHash));
        Assert.DoesNotContain("a-long-first-password", admin.PasswordHash);
    }

    [Fact]
    public async Task RunningItAgain_ChangesNothing()
    {
        await using var db = EmptyDb();
        await ProductionBootstrapper.RunAsync(db, Hasher, Valid());

        var second = await ProductionBootstrapper.RunAsync(db, Hasher, Valid());

        Assert.False(second.ShopCreated);
        Assert.False(second.AdminCreated);
        Assert.Equal(1, await db.Shops.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(5, await db.ServiceTypes.CountAsync());
    }

    [Fact]
    public async Task ItNeverOverwritesExistingData_EvenWithDifferentSettings()
    {
        await using var db = EmptyDb();
        await ProductionBootstrapper.RunAsync(db, Hasher, Valid());

        var other = Valid();
        other.AdminEmail = "intruder@example.com";
        other.ShopName = "Other Name";
        await ProductionBootstrapper.RunAsync(db, Hasher, other);

        Assert.Equal("Hyderabad Motors", (await db.Shops.SingleAsync()).Name);
        Assert.Equal("owner@garage.example", (await db.Users.SingleAsync()).Email);
    }

    [Fact]
    public async Task WithoutAdminSettings_TheShopIsStillCreated_ButNoUserIs()
    {
        await using var db = EmptyDb();
        var options = Valid();
        options.AdminEmail = null;
        options.AdminPassword = "";

        var result = await ProductionBootstrapper.RunAsync(db, Hasher, options);

        Assert.True(result.ShopCreated);
        Assert.False(result.AdminCreated);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task AShortAdminPassword_StopsTheStart_AndCreatesNoUser()
    {
        await using var db = EmptyDb();
        var options = Valid();
        options.AdminPassword = "short";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionBootstrapper.RunAsync(db, Hasher, options));

        Assert.Contains(ProductionBootstrapper.MinimumPasswordLength.ToString(), ex.Message);
        Assert.Empty(db.Users);
    }

    [Theory]
    [InlineData("9am", "18:00", "Asia/Kolkata")]
    [InlineData("09:00", "25:00", "Asia/Kolkata")]
    [InlineData("18:00", "09:00", "Asia/Kolkata")]
    [InlineData("09:00", "18:00", "Mars/Olympus_Mons")]
    public async Task InvalidShopSettings_StopTheStartWithAClearMessage_AndCreateNothing(string opening, string closing, string zone)
    {
        await using var db = EmptyDb();
        var options = Valid();
        options.OpeningTime = opening;
        options.ClosingTime = closing;
        options.TimeZoneId = zone;

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductionBootstrapper.RunAsync(db, Hasher, options));

        Assert.Empty(db.Shops);
        Assert.Empty(db.Users);
    }
}
