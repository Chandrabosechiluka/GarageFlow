using System.Text.Json;
using System.Text.Json.Serialization;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// Starts the WHOLE API in memory (real controllers, real auth, real middleware, real EF Core) against one throw-away
/// SQL Server database, shared by all the API tests. The tests then talk to it with an ordinary HttpClient, exactly as
/// a browser or mobile app would. The API creates the database and demo data itself because it runs in "Development".
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AllowedOrigin = "https://app.garageflow.test";
    private const string TestSigningKey = "integration-tests-signing-key-0123456789-abcdefghij";

    // JSON as the API sends it: camelCase names, enums as text.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, string?> _previousEnvironment = new();
    private string? _connectionString;

    public WebApplicationFactory<Program>? Factory { get; private set; }

    public async Task InitializeAsync()
    {
        if (!TestDatabase.IsAvailable)
        {
            var required = string.Equals(Environment.GetEnvironmentVariable("GARAGEFLOW_REQUIRE_SQL"), "true", StringComparison.OrdinalIgnoreCase);
            if (required)
                throw new InvalidOperationException("SQL Server is required (GARAGEFLOW_REQUIRE_SQL=true) but is not reachable.");
            return; // tests are skipped by RequiresSqlServerFact
        }

        var server = SqlServerLocator.ConnectionString!;
        _connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = $"GarageFlowApiTest_{Guid.NewGuid():N}" }.ConnectionString;

        // Environment variables beat appsettings AND the developer's user secrets, so these tests can never touch the
        // real development database. (Collection tests run one after another, so one set of variables is safe.)
        SetEnvironment("ConnectionStrings__GarageFlow", _connectionString);
        SetEnvironment("Jwt__SigningKey", TestSigningKey);
        SetEnvironment("Maintenance__Enabled", "false"); // no background worker while testing
        SetEnvironment("RateLimiting__LoginPermitLimit", "1000"); // the many test logins must not trip the limiter (its own tests lower it)
        SetEnvironment("Cors__AllowedOrigins__0", AllowedOrigin);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        using var warmUp = Factory.CreateClient(); // starts the app now: runs migrations and seeds the demo data
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        foreach (var (name, previous) in _previousEnvironment)
            Environment.SetEnvironmentVariable(name, previous);

        if (_connectionString is not null)
        {
            SqlConnection.ClearAllPools(); // pooled connections would otherwise keep the database "in use"
            await using var context = new GarageFlowDbContext(
                new DbContextOptionsBuilder<GarageFlowDbContext>().UseSqlServer(_connectionString).Options);
            await context.Database.EnsureDeletedAsync();
        }
    }

    private void SetEnvironment(string name, string value)
    {
        _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}

/// <summary>All tests in this collection share one running API and run one after another.</summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
}
