using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

public class InspectionItem : BaseEntity
{
    private InspectionItem()
    {
    }

    public InspectionItem(Guid appointmentId, string name, InspectionCondition condition, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Inspection item name is required.");

        AppointmentId = appointmentId;
        Name = name.Trim();
        Condition = condition;
        Notes = notes?.Trim();
    }

    public Guid AppointmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public InspectionCondition Condition { get; private set; }
    public string? Notes { get; private set; }
}
