using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class DailyRoutineHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public DailyRoutineHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task GetToday_ShouldExecuteRealPipelineThroughHttp()
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
            bootstrap.TryGetProperty("adultUserId", out _));

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

        var routineResponse =
            await client.GetAsync(
                "/api/v1/daily-routines/today");

        Assert.Equal(
            HttpStatusCode.OK,
            routineResponse.StatusCode);

        var routine =
            await routineResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            routine.TryGetProperty("date", out _));

        Assert.True(
            routine.TryGetProperty("status", out _));

        Assert.True(
            routine.TryGetProperty("tasks", out _));
    }
}
