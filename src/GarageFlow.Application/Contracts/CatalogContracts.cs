using System.ComponentModel.DataAnnotations;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Contracts;

public record ServiceTypeResponse(Guid Id, string Name, int DurationMinutes, Skill RequiredSkill);

public record TechnicianResponse(Guid Id, Guid ShopId, string Name, IReadOnlyList<Skill> Skills);

public record BayResponse(Guid Id, Guid ShopId, string Name);

// Requests for Admins to set up the shop. Validation attributes sit on the constructor parameters (required for records).
public record CreateTechnicianRequest(
    [Required, StringLength(200)] string Name,
    [Required, MinLength(1)] Skill[] Skills,
    Guid? ShopId = null);

public record CreateBayRequest(
    [Required, StringLength(100)] string Name,
    Guid? ShopId = null);

public record CreateServiceTypeRequest(
    [Required, StringLength(200)] string Name,
    [Range(5, 600)] int DurationMinutes,
    Skill RequiredSkill);
