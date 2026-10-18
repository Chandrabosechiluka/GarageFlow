using GarageFlow.Domain.Entities;

namespace GarageFlow.Application.Contracts;

/// <summary>Entity to DTO conversions, written by hand so every field is visible and easy to follow.</summary>
internal static class Mappings
{
    public static VehicleResponse ToResponse(this Vehicle v) =>
        new(v.Id, v.CustomerId, v.Make, v.Model, v.Year, v.RegistrationNumber);

    public static CustomerResponse ToResponse(this Customer c) =>
        new(c.Id, c.FullName, c.Email, c.Phone, c.Vehicles.Select(v => v.ToResponse()).ToList());

    public static ServiceTypeResponse ToResponse(this ServiceType s) =>
        new(s.Id, s.Name, s.DurationMinutes, s.RequiredSkill);

    public static TechnicianResponse ToResponse(this Technician t) =>
        new(t.Id, t.ShopId, t.Name, t.Skills.OrderBy(s => s).ToList());

    public static BayResponse ToResponse(this Bay b) => new(b.Id, b.ShopId, b.Name);

    public static InspectionItemResponse ToResponse(this InspectionItem i) =>
        new(i.Id, i.Name, i.Condition, i.Notes);

    public static AppointmentResponse ToResponse(this Appointment a) =>
        new(
            a.Id, a.ShopId, a.CustomerId, a.VehicleId, a.ServiceTypeId, a.TechnicianId, a.BayId,
            a.StartTimeUtc, a.EndTimeUtc, a.Status, a.CancellationReason,
            a.InspectionItems.Select(i => i.ToResponse()).ToList());
}
