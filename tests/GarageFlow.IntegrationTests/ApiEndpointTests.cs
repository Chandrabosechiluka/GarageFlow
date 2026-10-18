using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// End-to-end: real HTTP requests through routing, authentication, authorization, validation, the controllers,
/// the services, EF Core and SQL Server, and back through the error handling and logging middleware.
/// </summary>
[Collection("Api")]
public class ApiEndpointTests(ApiFixture api)
{
    private const string Password = "Garage@12345"; // the Development demo users
    private static int _nextDay = 30;

    private sealed record Ids(Guid CustomerId, Guid VehicleId, Guid BrakeServiceId, Guid AnilId, Guid Bay1Id);

    // Every test books on its own day, so tests in the shared database never fight over the same technician.
    private static int NextDay() => Interlocked.Increment(ref _nextDay);

    private static DateTime At(int day, int hourUtc) => DateTime.UtcNow.Date.AddDays(day).AddHours(hourUtc); // 05:00 UTC = 10:30 in India

    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = api.Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        // Show the server's own error text when it fails: a bare "500" tells you nothing.
        Assert.True(response.IsSuccessStatusCode, $"Login failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Ids> LoadIdsAsync(HttpClient client)
    {
        var customers = await client.GetFromJsonAsync<JsonElement>("/api/customers?search=demo", ApiFixture.Json);
        var customer = customers.GetProperty("items")[0];

        async Task<Guid> IdByName(string url, string name) =>
            (await client.GetFromJsonAsync<JsonElement>(url, ApiFixture.Json))
            .EnumerateArray().First(e => e.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();

        return new Ids(
            customer.GetProperty("id").GetGuid(),
            customer.GetProperty("vehicles")[0].GetProperty("id").GetGuid(),
            await IdByName("/api/service-types", "Brake Inspection"),
            await IdByName("/api/technicians", "Anil Reddy"),
            await IdByName("/api/bays", "Bay 1"));
    }

    private static object BookBody(Ids ids, DateTime start, bool pickResources = false) => new
    {
        customerId = ids.CustomerId,
        vehicleId = ids.VehicleId,
        serviceTypeId = ids.BrakeServiceId,
        technicianId = pickResources ? ids.AnilId : (Guid?)null, // empty = the system assigns one
        bayId = pickResources ? ids.Bay1Id : (Guid?)null,
        startTimeUtc = start
    };

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);

    private static async Task<Guid> BookAsync(HttpClient client, Ids ids, DateTime start)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", BookBody(ids, start));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await BodyAsync(response)).GetProperty("id").GetGuid();
    }

    // ---------- security and error format ----------

    [RequiresSqlServerFact]
    public async Task Health_IsOpenToEveryone()
    {
        var response = await api.Factory!.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresSqlServerFact]
    public async Task ProtectedEndpoint_WithoutAToken_Returns401AsProblemJson_WithACorrelationId()
    {
        var response = await api.Factory!.CreateClient().GetAsync("/api/appointments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await BodyAsync(response);
        var correlationId = body.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [RequiresSqlServerFact]
    public async Task Login_WithAWrongPassword_Returns401()
    {
        var response = await api.Factory!.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = "advisor@garageflow.local", password = "Wrong@12345" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal("Authentication failed", (await BodyAsync(response)).GetProperty("title").GetString());
    }

    [RequiresSqlServerFact]
    public async Task ACorrelationId_FromTheCaller_IsEchoed_AndAnUnsafeOneIsReplaced()
    {
        var client = api.Factory!.CreateClient();

        var good = new HttpRequestMessage(HttpMethod.Get, "/health");
        good.Headers.Add("X-Correlation-ID", "my-trace-123");
        Assert.Equal("my-trace-123", (await client.SendAsync(good)).Headers.GetValues("X-Correlation-ID").Single());

        var bad = new HttpRequestMessage(HttpMethod.Get, "/health");
        bad.Headers.Add("X-Correlation-ID", "not safe! <script>");
        var replaced = (await client.SendAsync(bad)).Headers.GetValues("X-Correlation-ID").Single();
        Assert.NotEqual("not safe! <script>", replaced);
        Assert.Matches("^[0-9a-f]{32}$", replaced);
    }

    [RequiresSqlServerFact]
    public async Task InvalidBody_Returns400WithFieldErrors_AndACorrelationId()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");

        var response = await advisor.PostAsJsonAsync("/api/customers", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await BodyAsync(response);
        Assert.True(body.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("correlationId").GetString()));
    }

    [RequiresSqlServerFact]
    public async Task UnknownAppointment_Returns404()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");

        var response = await advisor.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", (await BodyAsync(response)).GetProperty("title").GetString());
    }

    // ---------- roles ----------

    [RequiresSqlServerFact]
    public async Task ATechnician_CannotBook_ButAnAdvisorCan_AndCanReadItBack()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");
        var technician = await SignedInAsync("tech@garageflow.local");
        var ids = await LoadIdsAsync(advisor);
        var start = At(NextDay(), 5);

        var denied = await technician.PostAsJsonAsync("/api/appointments", BookBody(ids, start));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var created = await advisor.PostAsJsonAsync("/api/appointments", BookBody(ids, start));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var booked = await BodyAsync(created);
        Assert.Equal("Booked", booked.GetProperty("status").GetString()); // enums travel as text

        var id = booked.GetProperty("id").GetGuid();
        var again = await technician.GetFromJsonAsync<JsonElement>($"/api/appointments/{id}", ApiFixture.Json);
        Assert.Equal(id, again.GetProperty("id").GetGuid()); // anyone signed in may read
    }

    [RequiresSqlServerFact]
    public async Task BookingTheSameTechnicianAndBayTwice_Returns409()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");
        var ids = await LoadIdsAsync(advisor);
        var start = At(NextDay(), 5);

        var first = await advisor.PostAsJsonAsync("/api/appointments", BookBody(ids, start, pickResources: true));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await advisor.PostAsJsonAsync("/api/appointments", BookBody(ids, start, pickResources: true));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Conflict", (await BodyAsync(second)).GetProperty("title").GetString());
    }

    // ---------- pagination ----------

    [RequiresSqlServerFact]
    public async Task Lists_ArePaged_AndNonsensePagesAreRejected()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");
        var ids = await LoadIdsAsync(advisor);
        var day = NextDay();
        await BookAsync(advisor, ids, At(day, 5));
        await BookAsync(advisor, ids, At(day, 7));

        var page = await advisor.GetFromJsonAsync<JsonElement>("/api/appointments?page=1&pageSize=1", ApiFixture.Json);
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 2);
        Assert.True(page.GetProperty("hasNext").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.GetAsync("/api/appointments?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.GetAsync("/api/appointments?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.GetAsync("/api/customers?page=0")).StatusCode);
    }

    // ---------- lifecycle, audit trail ----------

    [RequiresSqlServerFact]
    public async Task RescheduleAndCancel_WorkOverHttp_AndEveryStepIsInTheAuditTrail()
    {
        var advisor = await SignedInAsync("advisor@garageflow.local");
        var admin = await SignedInAsync("admin@garageflow.local");
        var technician = await SignedInAsync("tech@garageflow.local");
        var ids = await LoadIdsAsync(advisor);
        var day = NextDay();
        var id = await BookAsync(advisor, ids, At(day, 5));

        var newStart = At(day, 7);
        var moved = await advisor.PostAsJsonAsync($"/api/appointments/{id}/reschedule", new { newStartTimeUtc = newStart });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(newStart, (await BodyAsync(moved)).GetProperty("startTimeUtc").GetDateTime().ToUniversalTime());

        // Only an Admin may lift the "2 hours notice" rule.
        var late = await advisor.PostAsJsonAsync($"/api/appointments/{id}/cancel", new { reason = "Customer called", allowLate = true });
        Assert.Equal(HttpStatusCode.Forbidden, late.StatusCode);

        var cancelled = await advisor.PostAsJsonAsync($"/api/appointments/{id}/cancel", new { reason = "Customer called" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await BodyAsync(cancelled)).GetProperty("status").GetString());

        var history = await advisor.GetFromJsonAsync<JsonElement>($"/api/appointments/{id}/history", ApiFixture.Json);
        Assert.Equal(new[] { "Created", "Updated", "Updated" }, history.EnumerateArray().Select(h => h.GetProperty("action").GetString()));
        Assert.All(history.EnumerateArray(), h => Assert.Equal("advisor@garageflow.local", h.GetProperty("performedBy").GetString()));
        Assert.Contains("Cancelled", history[2].GetProperty("details").GetString());

        // Who may read the audit trail?
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync($"/api/appointments/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await advisor.GetAsync("/api/audit")).StatusCode);
        var audit = await admin.GetFromJsonAsync<JsonElement>($"/api/audit?entityName=Appointment&entityId={id}", ApiFixture.Json);
        Assert.Equal(3, audit.GetProperty("totalCount").GetInt32());
    }
}
