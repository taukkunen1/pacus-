using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class HabitatDebugHttpTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public HabitatDebugHttpTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Debug_ChildHabitatGet()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var bootstrapRequest = new
        {
            adultName = $"Adulto {suffix}",
            adultEmail = $"adult-{suffix}@test.local",
            adultPassword = "Teste123!",
            childName = $"Crianca {suffix}",
            childPin = "1234"
        };

        var bootstrap = await client.PostAsJsonAsync(
            "/api/v1/bootstrap",
            bootstrapRequest);

        Console.WriteLine($"BOOTSTRAP: {(int)bootstrap.StatusCode}");
        Console.WriteLine(await bootstrap.Content.ReadAsStringAsync());

        var bootstrapBody =
            await bootstrap.Content.ReadFromJsonAsync<JsonElement>();

        var childUserId =
            bootstrapBody.GetProperty("childUserId").GetString();

        Console.WriteLine($"CHILD USER ID: {childUserId}");

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new
            {
                userId = childUserId,
                pin = bootstrapRequest.childPin
            });

        Console.WriteLine($"LOGIN: {(int)login.StatusCode}");
        var loginRaw = await login.Content.ReadAsStringAsync();
        Console.WriteLine(loginRaw);

        var loginBody =
            JsonSerializer.Deserialize<JsonElement>(loginRaw);

        var token =
            loginBody.GetProperty("token").GetString();

        Console.WriteLine(
            $"TOKEN EXISTS: {!string.IsNullOrWhiteSpace(token)}");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/v1/pacus/me/habitat");

        Console.WriteLine($"HABITAT GET: {(int)response.StatusCode}");
        Console.WriteLine(
            await response.Content.ReadAsStringAsync());
    }
}
