using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Tests;

public class ShopModelTests
{
    [Fact]
    public void Shop_TimeZoneId_IsRequiredAndDefaultsToIndia()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelTests;User Id=sa;Password=not-used;TrustServerCertificate=True")
            .Options;

        using var context = new GarageFlowDbContext(options);
        var property = context.Model.FindEntityType(typeof(Shop))!.FindProperty(nameof(Shop.TimeZoneId))!;

        Assert.False(property.IsNullable);
        // The default is what fills the column for shops that existed before the column was added.
        Assert.Equal("Asia/Kolkata", property.GetDefaultValue());
    }
}
