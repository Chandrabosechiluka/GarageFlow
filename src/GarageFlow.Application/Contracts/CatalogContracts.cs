using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Contracts;

public record ServiceTypeResponse(Guid Id, string Name, int DurationMinutes, Skill RequiredSkill);

public record TechnicianResponse(Guid Id, Guid ShopId, string Name, IReadOnlyList<Skill> Skills);

public record BayResponse(Guid Id, Guid ShopId, string Name);
