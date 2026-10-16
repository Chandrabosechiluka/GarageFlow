using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

public class Customer : BaseEntity
{
    private readonly List<Vehicle> _vehicles = new();

    private Customer()
    {
    }

    public Customer(string fullName, string email, string phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Phone number is required.");

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
    }

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles;

    public Vehicle AddVehicle(string make, string model, int year, string registrationNumber)
    {
        var normalised = Vehicle.NormaliseRegistration(registrationNumber);
        if (_vehicles.Any(v => v.RegistrationNumber == normalised))
            throw new DomainException($"Vehicle {normalised} is already registered for this customer.");

        var vehicle = new Vehicle(Id, make, model, year, registrationNumber);
        _vehicles.Add(vehicle);
        return vehicle;
    }
}
