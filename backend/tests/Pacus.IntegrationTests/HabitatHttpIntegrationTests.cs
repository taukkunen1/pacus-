using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class HabitatHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public HabitatHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Adult_ShouldGetAndUpdateHabitat_ThroughRealHttpPipeline()
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

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/api/v1/bootstrap",
            bootstrapRequest);

        Assert.Contains(
            bootstrapResponse.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

        var bootstrap =
            await bootstrapResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            bootstrap.TryGetProperty("childUserId", out var childUserIdElement));

        var childUserId = childUserIdElement.GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(childUserId));

        Assert.Contains(
            bootstrapResponse.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new
            {
                email = bootstrapRequest.adultEmail,
                password = bootstrapRequest.adultPassword
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login =
            await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            login.TryGetProperty("token", out var tokenElement));

        var token = tokenElement.GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var getResponse = await client.GetAsync(
            "/api/v1/pacus/me/habitat");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var initialHabitat =
            await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            initialHabitat.TryGetProperty("bounds", out _));

        Assert.True(
            initialHabitat.TryGetProperty("elements", out _));

        var updateRequest = new
        {
            bounds = new
            {
                width = 1200,
                height = 800
            },
            elements = new
            {
                water = true,
                plants = new[] { "plant-1", "plant-2" },
                rocks = new[] { "rock-1" },
                hidingSpots = new[] { "hide-1", "hide-2" },
                bubbles = true
            },
            theme = "aquatic"
        };

        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/habitat",
            updateRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var updatedHabitat =
            await updateResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            1200,
            updatedHabitat
                .GetProperty("bounds")
                .GetProperty("width")
                .GetDouble());

        Assert.Equal(
            800,
            updatedHabitat
                .GetProperty("bounds")
                .GetProperty("height")
                .GetDouble());

        Assert.Equal(
            "aquatic",
            updatedHabitat
                .GetProperty("theme")
                .GetString());

        var persistedResponse = await client.GetAsync(
            "/api/v1/pacus/me/habitat");

        Assert.Equal(
            HttpStatusCode.OK,
            persistedResponse.StatusCode);

        var persistedHabitat =
            await persistedResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            1200,
            persistedHabitat
                .GetProperty("bounds")
                .GetProperty("width")
                .GetDouble());

        Assert.Equal(
            800,
            persistedHabitat
                .GetProperty("bounds")
                .GetProperty("height")
                .GetDouble());

        Assert.Equal(
            "aquatic",
            persistedHabitat
                .GetProperty("theme")
                .GetString());

        var plants = persistedHabitat
            .GetProperty("elements")
            .GetProperty("plants");

        Assert.Equal(2, plants.GetArrayLength());

        var rocks = persistedHabitat
            .GetProperty("elements")
            .GetProperty("rocks");

        Assert.Equal(1, rocks.GetArrayLength());

        var hidingSpots = persistedHabitat
            .GetProperty("elements")
            .GetProperty("hidingSpots");

        Assert.Equal(2, hidingSpots.GetArrayLength());
    }

    [Fact]
    public async Task Child_ShouldReadHabitat_ButCannotUpdateIt()
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

        var bootstrapResponse = await client.PostAsJsonAsync(
            "/api/v1/bootstrap",
            bootstrapRequest);

        Assert.Contains(
            bootstrapResponse.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

        var bootstrap =
            await bootstrapResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            bootstrap.TryGetProperty("childUserId", out var childUserIdElement));

        var childUserId = childUserIdElement.GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(childUserId));

        Assert.Contains(
            bootstrapResponse.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

                var childLoginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new
            {
                userId = childUserId,
                pin = bootstrapRequest.childPin
            });

        Assert.Equal(
            HttpStatusCode.OK,
            childLoginResponse.StatusCode);

        var login =
            await childLoginResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            login.TryGetProperty("token", out var tokenElement));

        var token = tokenElement.GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var getResponse = await client.GetAsync(
            "/api/v1/pacus/me/habitat");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var updateRequest = new
        {
            bounds = new
            {
                width = 2000,
                height = 1200
            },
            elements = new
            {
                water = true,
                plants = new[] { "child-plant" },
                rocks = new[] { "child-rock" },
                hidingSpots = new[] { "child-hide" },
                bubbles = true
            },
            theme = "child-attempt"
        };

        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/habitat",
            updateRequest);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);
    }
}


