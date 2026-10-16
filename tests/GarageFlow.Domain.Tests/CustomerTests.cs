using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;

namespace GarageFlow.Domain.Tests;

public class CustomerTests
{
    private static Customer NewCustomer() => new("Asha Rao", "Asha@Example.com", "9876543210");

    [Fact]
    public void Constructor_NormalisesEmail()
    {
        var customer = NewCustomer();

        Assert.Equal("asha@example.com", customer.Email);
    }

    [Fact]
    public void Constructor_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() => new Customer("Asha Rao", "not-an-email", "9876543210"));
    }

    [Fact]
    public void AddVehicle_NormalisesRegistrationNumber()
    {
        var customer = NewCustomer();

        var vehicle = customer.AddVehicle("Maruti", "Swift", 2020, "ts 09-ab 1234");

        Assert.Equal("TS09AB1234", vehicle.RegistrationNumber);
        Assert.Single(customer.Vehicles);
    }

    [Fact]
    public void AddVehicle_WithDuplicateRegistration_Throws()
    {
        var customer = NewCustomer();
        customer.AddVehicle("Maruti", "Swift", 2020, "TS09AB1234");

        Assert.Throws<DomainException>(() =>
            customer.AddVehicle("Hyundai", "i20", 2021, "ts 09 ab 1234"));
    }
}
