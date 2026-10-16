using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

public class Vehicle : BaseEntity
{
    private Vehicle()
    {
    }

    public Vehicle(Guid customerId, string make, string model, int year, string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(make))
            throw new DomainException("Vehicle make is required.");
        if (string.IsNullOrWhiteSpace(model))
            throw new DomainException("Vehicle model is required.");
        if (year < 1980 || year > DateTime.UtcNow.Year + 1)
            throw new DomainException("Vehicle year is out of range.");

        CustomerId = customerId;
        Make = make.Trim();
        Model = model.Trim();
        Year = year;
        RegistrationNumber = NormaliseRegistration(registrationNumber);
    }

    public Guid CustomerId { get; private set; }
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public string RegistrationNumber { get; private set; } = string.Empty;

    /// <summary>"ts 09-ab 1234" and "TS09AB1234" are the same vehicle.</summary>
    public static string NormaliseRegistration(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new DomainException("Registration number is required.");

        return registrationNumber
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();
    }
}
