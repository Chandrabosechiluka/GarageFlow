using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

public class ServiceType : BaseEntity
{
    private ServiceType()
    {
    }

    public ServiceType(string name, int durationMinutes, Skill requiredSkill)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Service name is required.");
        if (durationMinutes <= 0)
            throw new DomainException("Service duration must be positive.");

        Name = name.Trim();
        DurationMinutes = durationMinutes;
        RequiredSkill = requiredSkill;
    }

    public string Name { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public Skill RequiredSkill { get; private set; }
}
