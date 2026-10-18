using System.Text.Json;
using System.Text.Json.Serialization;
using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GarageFlow.Infrastructure.Persistence;

public class GarageFlowDbContext : DbContext, IGarageFlowDbContext
{
    // Tables whose changes are written to the audit trail. (Reference data such as bays and services is not.)
    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(Appointment), typeof(InspectionItem), typeof(WaitlistEntry),
        typeof(Customer), typeof(Vehicle), typeof(AppUser)
    };

    // Never copied into the audit trail: secrets, and values that change on every save and tell nobody anything.
    private static readonly HashSet<string> HiddenProperties = new() { "PasswordHash", "RowVersion" };

    private static readonly JsonSerializerOptions AuditJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    private const int MaxDetailsLength = 2000; // the AuditLogs.Details column size

    private readonly ICurrentUser? _currentUser;
    private readonly IClock? _clock;

    /// <summary>
    /// The extra parameters are optional so plain "new GarageFlowDbContext(options)" (tests, tools) still works:
    /// without a user changes are recorded as made by "system".
    /// </summary>
    public GarageFlowDbContext(DbContextOptions<GarageFlowDbContext> options, ICurrentUser? currentUser = null, IClock? clock = null)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<Bay> Bays => Set<Bay>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<InspectionItem> InspectionItems => Set<InspectionItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GarageFlowDbContext).Assembly);
    }

    // Every way of saving (sync, async, with or without arguments) ends in one of these two methods.
    // The audit rows are added to the SAME unit of work, so they are saved in the SAME transaction as the change:
    // either both exist or neither does.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddAuditRows();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddAuditRows();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddAuditRows()
    {
        ChangeTracker.DetectChanges();

        var actor = string.IsNullOrWhiteSpace(_currentUser?.Email) ? "system" : _currentUser!.Email!;
        var now = _clock?.UtcNow ?? DateTime.UtcNow;

        var changed = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && AuditedTypes.Contains(e.Entity.GetType()))
            .ToList(); // materialised first: adding audit rows below changes the tracker

        foreach (var entry in changed)
        {
            var (action, details) = Describe(entry);
            if (action is null)
                continue; // marked modified, but nothing real changed

            var entityId = (Guid)entry.Property("Id").CurrentValue!;
            AuditLogs.Add(AuditLog.Create(entry.Entity.GetType().Name, entityId, action, actor, now, details));
        }
    }

    private static (string? Action, string? Details) Describe(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();

        switch (entry.State)
        {
            case EntityState.Added:
                foreach (var property in entry.Properties.Where(p => !HiddenProperties.Contains(p.Metadata.Name)))
                    values[property.Metadata.Name] = property.CurrentValue;
                return ("Created", Serialize(values));

            case EntityState.Modified:
                foreach (var property in entry.Properties.Where(p => p.IsModified && !HiddenProperties.Contains(p.Metadata.Name)))
                {
                    if (!Equals(property.OriginalValue, property.CurrentValue))
                        values[property.Metadata.Name] = new { from = property.OriginalValue, to = property.CurrentValue };
                }

                return values.Count == 0 ? (null, null) : ("Updated", Serialize(values));

            default:
                return ("Deleted", null);
        }
    }

    private static string Serialize(Dictionary<string, object?> values)
    {
        var json = JsonSerializer.Serialize(values, AuditJson);
        return json.Length <= MaxDetailsLength ? json : json[..(MaxDetailsLength - 3)] + "...";
    }
}
