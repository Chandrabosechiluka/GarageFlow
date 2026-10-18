using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// What a real deployment needs on top of "it works": the Admin can set the shop up, Swagger is closed in
/// production, only listed websites may call the API, and the login cannot be hammered.
/// </summary>
[Collection("Api")]
public class ProductionReadinessTests(ApiFixture api)
{
    private const string Password = "Garage@12345"; // the Development demo users

    private async Task<HttpClient> SignedInAsync(string email, WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? api.Factory!).CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.True(response.IsSuccessStatusCode, $"Login failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>The same API, started again with a few settings changed (added last, so they win).</summary>
    private WebApplicationFactory<Program> With(string? environment, params (string Key, string Value)[] settings) =>
        api.Factory!.WithWebHostBuilder(builder =>
        {
            if (environment is not null)
                builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))));
        });

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    // ---------- the Admin sets the shop up ----------

    [Fact]
    public async Task AnAdmin_CanAddATechnicianABayAndAService_AndEveryoneCanThenListThem()
    {
        var admin = await SignedInAsync("admin@garageflow.local");
        var techName = Unique("Tech");
        var bayName = Unique("Bay");
        var serviceName = Unique("Service");

        var tech = await admin.PostAsJsonAsync("/api/technicians", new { name = techName, skills = new[] { "Brakes", "Engine" } });
        var bay = await admin.PostAsJsonAsync("/api/bays", new { name = bayName });
        var service = await admin.PostAsJsonAsync("/api/service-types", new { name = serviceName, durationMinutes = 45, requiredSkill = "Tires" });

        Assert.Equal(HttpStatusCode.Created, tech.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bay.StatusCode);
        Assert.Equal(HttpStatusCode.Created, service.StatusCode);
        Assert.NotNull(tech.Headers.Location);

        var advisor = await SignedInAsync("advisor@garageflow.local");
        Assert.Contains(techName, await advisor.GetStringAsync("/api/technicians"));
        Assert.Contains(bayName, await advisor.GetStringAsync("/api/bays"));
        Assert.Contains(serviceName, await advisor.GetStringAsync("/api/service-types"));
    }

    [Theory]
    [InlineData("advisor@garageflow.local")]
    [InlineData("tech@garageflow.local")]
    public async Task OnlyAnAdmin_CanChangeTheCatalog(string email)
    {
        var client = await SignedInAsync(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/technicians", new { name = Unique("T"), skills = new[] { "Brakes" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/bays", new { name = Unique("B") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/service-types", new { name = Unique("S"), durationMinutes = 30, requiredSkill = "Tires" })).StatusCode);
    }

    [Fact]
    public async Task CatalogCreation_RejectsDuplicatesAndBadInput_WithTheRightStatus()
    {
        var admin = await SignedInAsync("admin@garageflow.local");
        var name = Unique("Bay");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/bays", new { name })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/bays", new { name = name.ToUpperInvariant() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/bays", new { name = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/technicians", new { name = Unique("T"), skills = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/service-types", new { name = Unique("S"), durationMinutes = 0, requiredSkill = "Tires" })).StatusCode);
    }

    // ---------- Swagger ----------

    [Fact]
    public async Task Swagger_IsOpenInDevelopment_ClosedInProduction_AndOpenInProductionOnlyIfSwitchedOn()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.Factory!.CreateClient().GetAsync("/swagger/v1/swagger.json")).StatusCode);

        using var production = With("Production");
        // "Closed" is 401 here, not 404: the secure-by-default rule asks for a login on every address, even unknown ones.
        var closed = (await production.CreateClient().GetAsync("/swagger/v1/swagger.json")).StatusCode;
        Assert.True(closed is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound, $"Swagger should be closed in Production but returned {(int)closed}");

        using var productionWithSwagger = With("Production", ("Swagger:Enabled", "true"));
        Assert.Equal(HttpStatusCode.OK, (await productionWithSwagger.CreateClient().GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    // ---------- headers and CORS ----------

    [Fact]
    public async Task EveryResponse_CarriesNoSniff()
    {
        var response = await api.Factory!.CreateClient().GetAsync("/health");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Cors_AllowsOnlyTheListedWebsite()
    {
        var client = api.Factory!.CreateClient();

        HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/customers");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization");
            return request;
        }

        var allowed = await client.SendAsync(Preflight(ApiFixture.AllowedOrigin));
        Assert.True(allowed.IsSuccessStatusCode, $"Preflight returned {(int)allowed.StatusCode}");
        Assert.Equal(ApiFixture.AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var stranger = await client.SendAsync(Preflight("https://evil.example"));
        Assert.False(stranger.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // ---------- login rate limit ----------

    private static Task<HttpResponseMessage> WrongLoginAsync(HttpClient client, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@garageflow.local", password = "definitely-wrong-password" })
        };
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task TooManyLoginAttempts_AreStoppedWith429_ButOtherEndpointsKeepWorking()
    {
        using var limited = With(null, ("RateLimiting:LoginPermitLimit", "3"));
        var client = limited.CreateClient();

        for (var attempt = 1; attempt <= 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await WrongLoginAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task BehindAProxy_EachRealCallerAddressGetsItsOwnLoginAllowance()
    {
        using var behindProxy = With(null, ("RateLimiting:LoginPermitLimit", "2"), ("Hosting:BehindProxy", "true"));
        var client = behindProxy.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await WrongLoginAsync(client, "203.0.113.10")).StatusCode);

        // A different person behind the same proxy is not punished for the first one's guesses.
        Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client, "203.0.113.99")).StatusCode);
    }

    [Fact]
    public async Task NotBehindAProxy_ForwardedForHeaderIsIgnored_SoItCannotBeUsedToDodgeTheLimit()
    {
        using var direct = With(null, ("RateLimiting:LoginPermitLimit", "2"));
        var client = direct.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WrongLoginAsync(client, "198.51.100.2")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await WrongLoginAsync(client, "198.51.100.3")).StatusCode);
    }
}
