using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class WaterHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;
    public WaterHttpIntegrationTests(MongoIntegrationFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task WaterEvent_IsIdempotent_AndUndoRestoresOnlyCurrentProfileTotal()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();
        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var id = Guid.NewGuid().ToString("N");
        var first = await adultClient.PostAsJsonAsync("/api/v1/water", new { amountMl = 250, eventId = id });
        var retry = await adultClient.PostAsJsonAsync("/api/v1/water", new { amountMl = 250, eventId = id });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var childAdd = await childClient.PostAsJsonAsync("/api/v1/water", new { amountMl = 500, eventId = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, childAdd.StatusCode);

        var adultToday = await adultClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        var childToday = await childClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(250, adultToday.GetProperty("totalMl").GetInt32());
        Assert.Single(adultToday.GetProperty("entries").EnumerateArray());
        Assert.Equal(500, childToday.GetProperty("totalMl").GetInt32());
        Assert.Single(childToday.GetProperty("entries").EnumerateArray());

        var undo = await adultClient.DeleteAsync("/api/v1/water/today/latest");
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        adultToday = await adultClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        childToday = await childClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(0, adultToday.GetProperty("totalMl").GetInt32());
        Assert.Equal(500, childToday.GetProperty("totalMl").GetInt32());
    }

    [Fact]
    public async Task AdultCanConfigureGoalPerProfile_AndProfilesHaveSeparateTotalsAndRewards()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();
        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var configured = await adultClient.PutAsJsonAsync(
            "/api/v1/settings/water",
            new { profileId = family.ChildUserId, goalMl = 1500, rewardPoints = 3 });
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);

        var adultAdd = await adultClient.PostAsJsonAsync(
            "/api/v1/water", new { amountMl = 250, eventId = Guid.NewGuid().ToString("N") });
        var childAdd1 = await childClient.PostAsJsonAsync(
            "/api/v1/water", new { amountMl = 500, eventId = Guid.NewGuid().ToString("N") });
        var childAdd2 = await childClient.PostAsJsonAsync(
            "/api/v1/water", new { amountMl = 1000, eventId = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, adultAdd.StatusCode);
        Assert.Equal(HttpStatusCode.OK, childAdd1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, childAdd2.StatusCode);

        var adultToday = await adultClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        var childToday = await childClient.GetFromJsonAsync<JsonElement>("/api/v1/water/today");
        Assert.Equal(250, adultToday.GetProperty("totalMl").GetInt32());
        Assert.Equal(2000, adultToday.GetProperty("goalMl").GetInt32());
        Assert.Equal(1500, childToday.GetProperty("totalMl").GetInt32());
        Assert.Equal(1500, childToday.GetProperty("goalMl").GetInt32());
        Assert.True(childToday.GetProperty("rewarded").GetBoolean());
        Assert.False(adultToday.GetProperty("rewarded").GetBoolean());

        var settings = await adultClient.GetFromJsonAsync<JsonElement>("/api/v1/settings/water");
        var childProfile = settings.GetProperty("profiles").EnumerateArray()
            .Single(profile => profile.GetProperty("profileId").GetString() == family.ChildUserId);
        Assert.Equal(1500, childProfile.GetProperty("goalMl").GetInt32());
        Assert.Equal(3, childProfile.GetProperty("rewardPoints").GetInt32());
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    public async Task AmountAbovePerEntryLimit_ReturnsBadRequest(int amountMl)
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();
        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/water", new { amountMl, eventId = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoalAboveConfiguredSafetyBound_ReturnsBadRequest()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();
        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PutAsJsonAsync(
            "/api/v1/settings/water",
            new { profileId = family.ChildUserId, goalMl = 4001, rewardPoints = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task LoginAdultAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new { email = family.AdultEmail, password = family.AdultPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    private static async Task LoginChildAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new { userId = family.ChildUserId, pin = family.ChildPin });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    private static async Task<TestFamily> BootstrapAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"water-{suffix}@test.local";
        const string password = "Teste123!";
        const string pin = "1234";
        var response = await client.PostAsJsonAsync("/api/v1/bootstrap", new
        {
            adultName = "Adulto Água", adultEmail = email, adultPassword = password,
            childName = "Criança Água", childPin = pin, responsibleConsent = true
        });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Created });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TestFamily(
            body.GetProperty("adultUserId").GetString()!,
            body.GetProperty("childUserId").GetString()!,
            email, password, pin);
    }

    private sealed record TestFamily(
        string AdultUserId, string ChildUserId, string AdultEmail, string AdultPassword, string ChildPin);
}
