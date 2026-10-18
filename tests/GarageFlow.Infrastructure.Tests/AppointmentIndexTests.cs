using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Tests;

public class AppointmentIndexTests
{
    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType AppointmentEntity()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelTests;User Id=sa;Password=not-used;TrustServerCertificate=True")
            .Options;

        using var context = new GarageFlowDbContext(options);
        return context.Model.FindEntityType(typeof(Appointment))!;
    }

    [Theory]
    [InlineData(nameof(Appointment.TechnicianId))]
    [InlineData(nameof(Appointment.BayId))]
    public void Appointment_HasAUniqueIndexOnResourceAndStartTime(string resourceProperty)
    {
        var expected = new[] { resourceProperty, nameof(Appointment.StartTimeUtc) };

        // The filter ("only active appointments") is verified for real against SQL Server in the integration tests.
        Assert.Single(AppointmentEntity().GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(expected));
    }
}
