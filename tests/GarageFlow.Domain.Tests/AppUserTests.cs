using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Tests;

public class AppUserTests
{
    [Fact]
    public void Create_NormalisesEmailAndStartsActive()
    {
        var user = new AppUser("  Admin@Example.COM ", " Demo Admin ", "hash", UserRole.Admin);

        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal("Demo Admin", user.DisplayName);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
    }

    [Theory]
    [InlineData("", "Name", "hash")]
    [InlineData("not-an-email", "Name", "hash")]
    [InlineData("a@b.com", " ", "hash")]
    [InlineData("a@b.com", "Name", "")]
    public void Create_WithMissingOrInvalidData_Throws(string email, string name, string hash)
    {
        Assert.Throws<DomainException>(() => new AppUser(email, name, hash, UserRole.Advisor));
    }

    [Fact]
    public void Create_WithUnknownRole_Throws()
    {
        Assert.Throws<DomainException>(() => new AppUser("a@b.com", "Name", "hash", (UserRole)99));
    }

    [Fact]
    public void Deactivate_MakesTheUserInactive()
    {
        var user = new AppUser("a@b.com", "Name", "hash", UserRole.Technician);

        user.Deactivate();

        Assert.False(user.IsActive);
    }
}
