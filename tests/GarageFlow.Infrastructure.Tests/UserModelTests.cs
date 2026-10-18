using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Tests;

public class UserModelTests
{
    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType UserEntity()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelTests;User Id=sa;Password=not-used;TrustServerCertificate=True")
            .Options;

        using var context = new GarageFlowDbContext(options);
        return context.Model.FindEntityType(typeof(AppUser))!;
    }

    [Fact]
    public void AppUser_IsMapped()
    {
        Assert.NotNull(UserEntity());
    }

    [Fact]
    public void AppUser_Email_IsUnique()
    {
        var index = Assert.Single(UserEntity().GetIndexes(), i => i.Properties.Any(p => p.Name == nameof(AppUser.Email)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void AppUser_Role_IsStoredAsText()
    {
        var property = UserEntity().FindProperty(nameof(AppUser.Role))!;

        Assert.Equal(typeof(string), property.GetProviderClrType());
    }
}
