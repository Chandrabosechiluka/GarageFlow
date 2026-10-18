using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Tests;

public class WaitlistModelTests
{
    private static GarageFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelTests;User Id=sa;Password=not-used;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void WaitlistEntry_UsesARowVersion_AndStoresStatusAsText()
    {
        using var context = NewContext();
        var entity = context.Model.FindEntityType(typeof(WaitlistEntry))!;

        Assert.True(entity.FindProperty(nameof(WaitlistEntry.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(typeof(string), entity.FindProperty(nameof(WaitlistEntry.Status))!.GetProviderClrType());
        Assert.Equal("WaitlistEntries", entity.GetTableName());
    }

    [Fact]
    public void WaitlistEntry_ForeignKeys_NeverCascade()
    {
        using var context = NewContext();
        var entity = context.Model.FindEntityType(typeof(WaitlistEntry))!;

        Assert.NotEmpty(entity.GetForeignKeys());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
