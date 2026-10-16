using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GarageFlow.Infrastructure.Tests;

/// <summary>
/// Checks the EF Core mapping without needing a running database: building the model
/// never opens a connection, so these tests are fast and run anywhere.
/// </summary>
public class ModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelTests;User Id=sa;Password=not-used;TrustServerCertificate=True")
            .Options;

        using var context = new GarageFlowDbContext(options);
        return context.Model;
    }

    [Fact]
    public void AllDomainEntities_AreMapped()
    {
        var model = BuildModel();

        var expected = new[]
        {
            typeof(Shop), typeof(Customer), typeof(Vehicle), typeof(ServiceType), typeof(Technician),
            typeof(Bay), typeof(Appointment), typeof(InspectionItem), typeof(AuditLog)
        };

        foreach (var type in expected)
            Assert.NotNull(model.FindEntityType(type));
    }

    [Fact]
    public void Appointment_Status_IsStoredAsText()
    {
        var property = BuildModel().FindEntityType(typeof(Appointment))!.FindProperty(nameof(Appointment.Status))!;

        Assert.Equal(typeof(string), property.GetProviderClrType());
    }

    [Fact]
    public void Appointment_RowVersion_IsAConcurrencyToken()
    {
        var property = BuildModel().FindEntityType(typeof(Appointment))!.FindProperty(nameof(Appointment.RowVersion))!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void Appointment_ForeignKeys_NeverCascadeDelete()
    {
        var appointment = BuildModel().FindEntityType(typeof(Appointment))!;
        var foreignKeys = appointment.GetForeignKeys().ToList();

        Assert.Equal(6, foreignKeys.Count); // shop, customer, vehicle, service type, technician, bay
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void InspectionItems_AreDeletedWithTheirAppointment()
    {
        var item = BuildModel().FindEntityType(typeof(InspectionItem))!;
        var foreignKey = Assert.Single(item.GetForeignKeys());

        Assert.Equal(typeof(Appointment), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Technician_Skills_AreStoredInOneColumn()
    {
        var technician = BuildModel().FindEntityType(typeof(Technician))!;

        Assert.Null(technician.FindProperty(nameof(Technician.Skills)));
        Assert.NotNull(technician.FindProperty("SkillsStorage"));
    }

    [Fact]
    public void Customer_Email_IsUnique()
    {
        var customer = BuildModel().FindEntityType(typeof(Customer))!;

        var index = Assert.Single(customer.GetIndexes(), i => i.Properties.Any(p => p.Name == nameof(Customer.Email)));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Vehicle_RegistrationNumber_IsUniquePerCustomer()
    {
        var vehicle = BuildModel().FindEntityType(typeof(Vehicle))!;

        var index = Assert.Single(
            vehicle.GetIndexes(),
            i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Vehicle.CustomerId), nameof(Vehicle.RegistrationNumber) }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void AppointmentTimes_ComeBackFromTheDatabaseAsUtc()
    {
        var property = BuildModel().FindEntityType(typeof(Appointment))!.FindProperty(nameof(Appointment.StartTimeUtc))!;
        var converter = property.GetValueConverter();
        Assert.NotNull(converter);

        var unspecified = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Unspecified);
        var loaded = (DateTime)converter!.ConvertFromProvider(unspecified)!;

        Assert.Equal(DateTimeKind.Utc, loaded.Kind);
        Assert.Equal(unspecified.Ticks, loaded.Ticks);
    }
}
