using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Persistence;

public class GarageFlowDbContext : DbContext
{
    public GarageFlowDbContext(DbContextOptions<GarageFlowDbContext> options) : base(options)
    {
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> class in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GarageFlowDbContext).Assembly);
    }
}
