using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

public class Bay : BaseEntity
{
    private Bay()
    {
    }

    public Bay(Guid shopId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Bay name is required.");

        ShopId = shopId;
        Name = name.Trim();
    }

    public Guid ShopId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
