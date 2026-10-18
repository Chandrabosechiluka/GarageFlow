using System.ComponentModel.DataAnnotations;

namespace GarageFlow.Application.Contracts;

// DTOs ("data transfer objects") are the shapes that go over HTTP. They are separate from the domain
// entities so the API contract can change without touching business rules, and entities are never exposed directly.
// The [property: ...] attributes are checked automatically by [ApiController] before the action runs (HTTP 400 if invalid).

public record CreateCustomerRequest(
    [property: Required, StringLength(200)] string FullName,
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(30)] string Phone);

public record AddVehicleRequest(
    [property: Required, StringLength(100)] string Make,
    [property: Required, StringLength(100)] string Model,
    [property: Range(1980, 2100)] int Year,
    [property: Required, StringLength(20)] string RegistrationNumber);

public record VehicleResponse(Guid Id, Guid CustomerId, string Make, string Model, int Year, string RegistrationNumber);

public record CustomerResponse(Guid Id, string FullName, string Email, string Phone, IReadOnlyList<VehicleResponse> Vehicles);
