using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class WaterHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;
    public WaterHttpIntegrationTests(MongoIntegrationFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task WaterEvent_IsIdempotent_AndUndoRestoresTotal()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();
        await BootstrapAndLoginAdultAsync(client);

        var id = Guid.NewGuid().ToString("N");
        var first = await client.PostAsJsonAsync("/api/v1/water", new { amountMl = 250, eventId = id });
        var retry = await client.PostAsJsonAsync("/api/v1/water", new { amountMl = 250, eventId = id });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var today = await client.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(250, today.GetProperty("totalMl").GetInt32());
        Assert.Single(today.GetProperty("entries").EnumerateArray());

        var undo = await client.DeleteAsync("/api/v1/water/today/latest");
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        today = await client.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(0, today.GetProperty("totalMl").GetInt32());
    }

    [Fact]
    public async Task AdultCanChangeGoal_AndTodayUsesConfiguredGoal()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();
        await BootstrapAndLoginAdultAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(
            "/api/v1/settings/water", new { goalMl = 1800 })).StatusCode);

        var today = await client.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(1800, today.GetProperty("goalMl").GetInt32());
    }

    private static async Task BootstrapAndLoginAdultAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"water-{suffix}@test.local";
        var password = string.Concat("Teste", 123, (char)33);
        var bootstrap = await client.PostAsJsonAsync("/api/v1/bootstrap", new
        {
            adultName = "Adulto Agua", adultEmail = email, adultPassword = password,
            childName = "Crianca Agua", childPin = "1234", responsibleConsent = true
        });
        Assert.Contains(bootstrap.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Created });
        var login = await client.PostAsJsonAsync("/api/v1/auth/adult/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }
}
