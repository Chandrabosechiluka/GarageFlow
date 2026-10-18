using GarageFlow.Application.Services;
using GarageFlow.Infrastructure;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GarageFlow.IntegrationTests;

/// <summary>Finds the SQL Server these tests may use, or null when it is not reachable (the tests are then skipped).</summary>
internal static class SqlServerLocator
{
    private static readonly Lazy<string?> Server = new(Resolve);

    public static string? ConnectionString => Server.Value;

    private static string? Resolve()
    {
        // 1) an explicit override, 2) the same connection string the API uses locally (dotnet user-secrets).
        var candidate = Environment.GetEnvironmentVariable("GARAGEFLOW_TEST_SQL");
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = new ConfigurationBuilder().AddUserSecrets("garageflow-api-6f1c2d84").Build()["ConnectionStrings:GarageFlow"];

        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        try
        {
            var probe = new SqlConnectionStringBuilder(candidate) { InitialCatalog = "master", ConnectTimeout = 3 };
            using var connection = new SqlConnection(probe.ConnectionString);
            connection.Open();
            return candidate;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Like [Fact], but skipped (not failed) when SQL Server is not running.</summary>
public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    public RequiresSqlServerFactAttribute()
    {
        // In CI the database is always supposed to be there: GARAGEFLOW_REQUIRE_SQL=true turns "skip" into "fail loudly",
        // so a broken CI setup can never hide behind a green build of skipped tests.
        var required = string.Equals(Environment.GetEnvironmentVariable("GARAGEFLOW_REQUIRE_SQL"), "true", StringComparison.OrdinalIgnoreCase);
        if (!TestDatabase.IsAvailable && !required)
            Skip = "SQL Server is not reachable. Start it with 'docker compose up -d' (the connection string comes from the API's user secrets, or GARAGEFLOW_TEST_SQL).";
    }
}

public record SeedIds(
    Guid ShopId, Guid CustomerId, Guid VehicleId, Guid BrakeServiceId, Guid GeneralServiceId,
    Guid AnilId, Guid Bay1Id, Guid Bay2Id);

/// <summary>
/// A throw-away database on the real SQL Server: created with the real migrations, filled with the demo data,
/// and deleted afterwards. Every test gets its own, so tests cannot disturb each other or your development data.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _connectionString;

    private TestDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static bool IsAvailable => SqlServerLocator.ConnectionString is not null;

    public static async Task<TestDatabase> CreateAsync()
    {
        var server = SqlServerLocator.ConnectionString
            ?? throw new InvalidOperationException("SQL Server is not available.");

        var builder = new SqlConnectionStringBuilder(server) { InitialCatalog = $"GarageFlowTest_{Guid.NewGuid():N}" };
        var database = new TestDatabase(builder.ConnectionString);

        await using var context = database.NewContext();
        await context.Database.MigrateAsync(); // also proves the migrations build a working database
        await DbSeeder.SeedAsync(context);

        return database;
    }

    public GarageFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<GarageFlowDbContext>().UseSqlServer(_connectionString).Options);

    /// <summary>A fresh service with its OWN context and its own gate, like one web request would get.</summary>
    public AppointmentService NewAppointmentService(out GarageFlowDbContext context)
    {
        context = NewContext();
        return new AppointmentService(context, new SystemClock(), new SqlServerBookingGate(context));
    }

    public async Task<SeedIds> LoadIdsAsync()
    {
        await using var context = NewContext();
        return new SeedIds(
            (await context.Shops.SingleAsync()).Id,
            (await context.Customers.SingleAsync()).Id,
            (await context.Vehicles.SingleAsync()).Id,
            (await context.ServiceTypes.SingleAsync(s => s.Name == "Brake Inspection")).Id,
            (await context.ServiceTypes.SingleAsync(s => s.Name == "General Service")).Id,
            (await context.Technicians.SingleAsync(t => t.Name == "Anil Reddy")).Id,
            (await context.Bays.SingleAsync(b => b.Name == "Bay 1")).Id,
            (await context.Bays.SingleAsync(b => b.Name == "Bay 2")).Id);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools(); // pooled connections would otherwise keep the database "in use"
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }
}
