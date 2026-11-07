using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GarageFlow.IntegrationTests;

/// <summary>
/// The diagnostics lab creates problems on purpose (a memory leak, a CPU spike...), so the important tests are
/// that it stays switched OFF unless someone deliberately turns it on, and that only an Admin can use it.
/// </summary>
[Collection("Api")]
public class DiagnosticsLabTests(ApiFixture api)
{
    private const string Password = "Garage@12345"; // the Development demo users

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.True(response.IsSuccessStatusCode, $"Login failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private WebApplicationFactory<Program> LabSwitchedOn() =>
        api.Factory!.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["DiagnosticsLab:Enabled"] = "true" })));

    [RequiresSqlServerFact]
    public async Task TheLab_IsOffByDefault_EvenForAnAdmin()
    {
        var admin = await SignedInAsync(api.Factory!, "admin@garageflow.local");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/lab/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/lab/leak?entries=1", null)).StatusCode);
    }

    [RequiresSqlServerFact]
    public async Task WhenSwitchedOn_OnlyAnAdmin_CanUseIt()
    {
        using var factory = LabSwitchedOn();

        var advisor = await SignedInAsync(factory, "advisor@garageflow.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await advisor.GetAsync("/api/lab/status")).StatusCode);

        var admin = await SignedInAsync(factory, "admin@garageflow.local");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/lab/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/lab/non-blocking?milliseconds=100")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/lab/slow-query?seconds=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync("/api/lab/leak")).StatusCode);
    }
}
