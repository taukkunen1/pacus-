using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class HealthHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public HealthHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Health_ShouldReturnOk_WhenMongoIsAvailable()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response =
            await client.GetAsync("/api/v1/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "ok",
            body.GetProperty("status").GetString());

        Assert.Equal(
            "connected",
            body.GetProperty("database").GetString());

        Assert.True(
            body.TryGetProperty("timestamp", out _));
    }
}
