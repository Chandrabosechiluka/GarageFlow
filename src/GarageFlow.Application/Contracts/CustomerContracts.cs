using System.ComponentModel.DataAnnotations;

namespace GarageFlow.Application.Contracts;

// DTOs ("data transfer objects") are the shapes that go over HTTP. They are separate from the domain
// entities so the API contract can change without touching business rules, and entities are never exposed directly.
// The [...] attributes are checked automatically by [ApiController] before the action runs (HTTP 400 if invalid).

public record CreateCustomerRequest(
    [Required, StringLength(200)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(30)] string Phone);

public record AddVehicleRequest(
    [Required, StringLength(100)] string Make,
    [Required, StringLength(100)] string Model,
    [Range(1980, 2100)] int Year,
    [Required, StringLength(20)] string RegistrationNumber);

public record VehicleResponse(Guid Id, Guid CustomerId, string Make, string Model, int Year, string RegistrationNumber);

public record CustomerResponse(Guid Id, string FullName, string Email, string Phone, IReadOnlyList<VehicleResponse> Vehicles);
