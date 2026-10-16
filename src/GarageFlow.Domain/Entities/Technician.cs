using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

public class Technician : BaseEntity
{
    private readonly HashSet<Skill> _skills = new();

    private Technician()
    {
    }

    public Technician(Guid shopId, string name, IEnumerable<Skill> skills)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Technician name is required.");

        var skillList = skills?.ToList() ?? new List<Skill>();
        if (skillList.Count == 0)
            throw new DomainException("A technician needs at least one skill.");

        ShopId = shopId;
        Name = name.Trim();
        foreach (var skill in skillList)
            _skills.Add(skill);
    }

    public Guid ShopId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public IReadOnlyCollection<Skill> Skills => _skills;

    public bool HasSkill(Skill skill) => _skills.Contains(skill);
}
