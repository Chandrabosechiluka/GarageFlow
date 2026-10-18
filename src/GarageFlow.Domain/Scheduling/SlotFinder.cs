using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;

namespace GarageFlow.Domain.Scheduling;

/// <summary>An existing active appointment, reduced to what scheduling needs: who and what is busy, and when.</summary>
public record BusyPeriod(Guid TechnicianId, Guid BayId, DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>Half-open [start, end): a period ending at 10:00 does not clash with one starting at 10:00.</summary>
    public bool Overlaps(DateTime startUtc, DateTime endUtc) => StartUtc < endUtc && startUtc < EndUtc;
}

/// <summary>A start time at which the service can be done, with every technician and bay that is free for it.</summary>
public record AvailableSlot(DateTime StartUtc, DateTime EndUtc, IReadOnlyList<Guid> TechnicianIds, IReadOnlyList<Guid> BayIds);

/// <summary>
/// Pure scheduling logic: no database, no clock, no framework. Everything it needs is passed in,
/// which makes it easy to test exhaustively. The Application layer loads the data and calls this.
/// </summary>
public static class SlotFinder
{
    public const int DefaultStepMinutes = 30;

    public static IReadOnlyList<Guid> FreeTechnicians(
        IEnumerable<Technician> candidates, IReadOnlyCollection<BusyPeriod> busy, DateTime startUtc, DateTime endUtc) =>
        candidates
            .Where(t => !busy.Any(b => b.TechnicianId == t.Id && b.Overlaps(startUtc, endUtc)))
            .OrderBy(t => t.Name, StringComparer.Ordinal) // deterministic: the same input always picks the same person
            .Select(t => t.Id)
            .ToList();

    public static IReadOnlyList<Guid> FreeBays(
        IEnumerable<Bay> candidates, IReadOnlyCollection<BusyPeriod> busy, DateTime startUtc, DateTime endUtc) =>
        candidates
            .Where(b => !busy.Any(p => p.BayId == b.Id && p.Overlaps(startUtc, endUtc)))
            .OrderBy(b => b.Name, StringComparer.Ordinal)
            .Select(b => b.Id)
            .ToList();

    /// <summary>
    /// Walks through the shop's opening hours on one local day in fixed steps and keeps every start time where
    /// a qualified technician AND a bay are both free for the full duration of the service.
    /// </summary>
    public static IReadOnlyList<AvailableSlot> FindSlots(
        Shop shop,
        ServiceType service,
        IEnumerable<Technician> technicians,
        IEnumerable<Bay> bays,
        IReadOnlyCollection<BusyPeriod> busy,
        DateOnly localDate,
        DateTime nowUtc,
        int stepMinutes = DefaultStepMinutes)
    {
        if (stepMinutes <= 0)
            throw new DomainException("The slot step must be positive.");

        var qualified = technicians.Where(t => t.ShopId == shop.Id && t.HasSkill(service.RequiredSkill)).ToList();
        var shopBays = bays.Where(b => b.ShopId == shop.Id).ToList();

        var (open, close) = shop.GetOpenWindowUtc(localDate);
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);

        var slots = new List<AvailableSlot>();
        for (var start = open; start + duration <= close; start = start.AddMinutes(stepMinutes))
        {
            if (start <= nowUtc)
                continue; // never offer the past

            var end = start + duration;

            var freeTechnicians = FreeTechnicians(qualified, busy, start, end);
            if (freeTechnicians.Count == 0)
                continue;

            var freeBays = FreeBays(shopBays, busy, start, end);
            if (freeBays.Count == 0)
                continue;

            slots.Add(new AvailableSlot(start, end, freeTechnicians, freeBays));
        }

        return slots;
    }
}
