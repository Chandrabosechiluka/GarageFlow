using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Infrastructure.Persistence;

/// <summary>
/// Settings for the very first start of a real environment. They come from configuration (in a container: the
/// environment variables Bootstrap__AdminEmail, Bootstrap__AdminPassword, ...), never from the repository.
/// </summary>
public sealed class BootstrapOptions
{
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminDisplayName { get; set; } = "Administrator";

    public string ShopName { get; set; } = "GarageFlow";
    public string OpeningTime { get; set; } = "09:00";
    public string ClosingTime { get; set; } = "18:00";
    public string TimeZoneId { get; set; } = Shop.DefaultTimeZoneId;
}

public sealed record BootstrapResult(bool ShopCreated, bool AdminCreated);

/// <summary>
/// Makes a brand-new, empty production database usable: one shop with the standard services, and one Admin who can
/// then create technicians, bays and other users through the API. Replaces the Development-only demo data.
/// Safe to run on every start: it only fills in what is missing and never overwrites anything.
/// </summary>
public static class ProductionBootstrapper
{
    /// <summary>A short password is the easiest way into a public API, so the first admin must have a long one.</summary>
    public const int MinimumPasswordLength = 12;

    public static async Task<BootstrapResult> RunAsync(
        GarageFlowDbContext db, IPasswordHasher hasher, BootstrapOptions options, ILogger? logger = null, CancellationToken ct = default)
    {
        var shopCreated = false;
        if (!await db.Shops.AnyAsync(ct))
        {
            var opening = ParseTime(options.OpeningTime, nameof(options.OpeningTime));
            var closing = ParseTime(options.ClosingTime, nameof(options.ClosingTime));
            try
            {
                db.Shops.Add(new Shop(options.ShopName, opening, closing, options.TimeZoneId));
            }
            catch (Exception ex) when (ex is GarageFlow.Domain.Common.DomainException)
            {
                // A typo in configuration should stop the start with a clear message, not create a half-working shop.
                throw new InvalidOperationException($"Bootstrap settings are invalid: {ex.Message}", ex);
            }

            if (!await db.ServiceTypes.AnyAsync(ct))
                db.ServiceTypes.AddRange(StandardServiceTypes());

            await db.SaveChangesAsync(ct);
            shopCreated = true;
            logger?.LogInformation("Bootstrap: created shop '{Shop}' with the standard services.", options.ShopName);
        }

        var adminCreated = false;
        if (!await db.Users.AnyAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
            {
                // Do not crash (the platform would restart the container forever); say it loudly instead.
                logger?.LogWarning("Bootstrap: there are no users and Bootstrap:AdminEmail / Bootstrap:AdminPassword are not set, so nobody can log in.");
            }
            else
            {
                if (options.AdminPassword.Length < MinimumPasswordLength)
                    throw new InvalidOperationException($"Bootstrap:AdminPassword must be at least {MinimumPasswordLength} characters.");

                db.Users.Add(new AppUser(options.AdminEmail, options.AdminDisplayName, hasher.Hash(options.AdminPassword), UserRole.Admin));
                await db.SaveChangesAsync(ct);
                adminCreated = true;
                // The password is deliberately not logged.
                logger?.LogInformation("Bootstrap: created the first Admin account {Email}. Remove Bootstrap__AdminPassword from the environment now.", options.AdminEmail.Trim().ToLowerInvariant());
            }
        }

        return new BootstrapResult(shopCreated, adminCreated);
    }

    /// <summary>The services every garage offers. Shared with the Development demo data.</summary>
    public static IEnumerable<ServiceType> StandardServiceTypes() => new[]
    {
        new ServiceType("Oil Change", 45, Skill.OilChange),
        new ServiceType("Brake Inspection", 60, Skill.Brakes),
        new ServiceType("Tyre Rotation", 30, Skill.Tires),
        new ServiceType("Engine Diagnostics", 90, Skill.Engine),
        new ServiceType("General Service", 120, Skill.General)
    };

    private static TimeOnly ParseTime(string value, string setting) =>
        TimeOnly.TryParseExact(value?.Trim(), new[] { "HH:mm", "H:mm" }, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var time)
            ? time
            : throw new InvalidOperationException($"Bootstrap:{setting} must look like 09:00 (24-hour clock), but was '{value}'.");
}
