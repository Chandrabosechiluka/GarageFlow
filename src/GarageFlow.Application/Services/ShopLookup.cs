using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

/// <summary>
/// Finds the shop a request is about. GarageFlow has a single shop today, so an omitted shopId means "the shop".
/// With several shops the caller must say which one.
/// </summary>
internal static class ShopLookup
{
    public static async Task<Shop> ResolveAsync(IGarageFlowDbContext db, Guid? shopId, CancellationToken ct)
    {
        if (shopId.HasValue)
        {
            var id = shopId.Value;
            return await db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new NotFoundException(nameof(Shop), id);
        }

        var shops = await db.Shops.AsNoTracking().Take(2).ToListAsync(ct);
        return shops.Count switch
        {
            0 => throw new DomainException("No shop is configured."),
            1 => shops[0],
            _ => throw new DomainException("There is more than one shop; please say which one with shopId.")
        };
    }
}
