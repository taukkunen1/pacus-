using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.IntegrationTests;

public sealed class HistoryHttpIntegrationTests
    : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public HistoryHttpIntegrationTests(
        MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task GetHistory_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync(
                "/api/v1/history");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WithExistingDate_ShouldReturnRoutine()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        // GetOrCreateTodayAsync (chamado por EnsureTodayRoutineAsync) usa o
        // timezone fixo "America/Sao_Paulo" pra decidir que dia e "hoje" (ver
        // DailyRoutinesController), nao UTC -- calcular "hoje" com
        // DateTime.UtcNow aqui quebra sempre que o teste roda entre 00h-03h
        // UTC (21h-00h em SP), quando os dois dias divergem. Usando o mesmo
        // helper que o codigo de producao usa pra ficar consistente.
        var today = TimezoneHelper.GetOperationalDate("America/Sao_Paulo");

        await EnsureTodayRoutineAsync(client);

        var response =
            await client.GetAsync(
                $"/api/v1/history?date={today}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var routine =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            today,
            routine
                .GetProperty("date")
                .GetString());
    }

    [Fact]
    public async Task GetHistory_WithMissingDate_ShouldReturnNotFound()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var response =
            await client.GetAsync(
                "/api/v1/history?date=1990-01-01");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_WithDateRange_ShouldReturnFamilyHistory()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        await EnsureTodayRoutineAsync(client);

        // GetOrCreateTodayAsync (chamado por EnsureTodayRoutineAsync) usa o
        // timezone fixo "America/Sao_Paulo" pra decidir que dia e "hoje" (ver
        // DailyRoutinesController), nao UTC -- calcular "hoje" com
        // DateTime.UtcNow aqui quebra sempre que o teste roda entre 00h-03h
        // UTC (21h-00h em SP), quando os dois dias divergem. Usando o mesmo
        // helper que o codigo de producao usa pra ficar consistente.
        var today = TimezoneHelper.GetOperationalDate("America/Sao_Paulo");

        var response =
            await client.GetAsync(
                $"/api/v1/history?from={today}&to={today}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Endpoint paginado (achado #4 da auditoria de API de 2026-09-01 -- ver
        // docs/ESTADO_ATUAL.md): a resposta agora e um PagedResult, nao mais um
        // array solto.
        Assert.Equal(
            JsonValueKind.Array,
            body.GetProperty("items").ValueKind);
    }

    [Fact]
    public async Task GetHistory_ShouldNotReturnAnotherFamilyData()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var familyOne =
            await BootstrapAsync(client);

        await LoginAdultAsync(
            client,
            familyOne);

        await EnsureTodayRoutineAsync(client);

        // GetOrCreateTodayAsync (chamado por EnsureTodayRoutineAsync) usa o
        // timezone fixo "America/Sao_Paulo" pra decidir que dia e "hoje" (ver
        // DailyRoutinesController), nao UTC -- calcular "hoje" com
        // DateTime.UtcNow aqui quebra sempre que o teste roda entre 00h-03h
        // UTC (21h-00h em SP), quando os dois dias divergem. Usando o mesmo
        // helper que o codigo de producao usa pra ficar consistente.
        var today = TimezoneHelper.GetOperationalDate("America/Sao_Paulo");

        var familyTwo =
            await BootstrapAsync(client);

        await LoginAdultAsync(
            client,
            familyTwo);

        var response =
            await client.GetAsync(
                $"/api/v1/history?date={today}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task EnsureTodayRoutineAsync(
        HttpClient client)
    {
        var response =
            await client.GetAsync(
                "/api/v1/daily-routines/today");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private async Task LoginAdultAsync(
        HttpClient client,
        TestFamily family)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/v1/auth/adult/login",
                new
                {
                    email = family.AdultEmail,
                    password = family.AdultPassword
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var token =
            body
                .GetProperty("token")
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);
    }

    private static async Task<TestFamily> BootstrapAsync(
        HttpClient client)
    {
        var suffix =
            Guid.NewGuid()
                .ToString("N")[..8];

        var adultEmail =
            $"adult-{suffix}@test.local";

        const string adultPassword =
            "Teste123!";

        const string childPin =
            "1234";

        var response =
            await client.PostAsJsonAsync(
                "/api/v1/bootstrap",
                new
                {
                    adultName =
                        $"Adulto {suffix}",

                    adultEmail,

                    adultPassword,

                    childName =
                        $"Crianca {suffix}",

                    childPin,

                    responsibleConsent = true
                });
                });

        Assert.Contains(
            response.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        return new TestFamily(
            body
                .GetProperty("adultUserId")
                .GetString()!,

            body
                .GetProperty("childUserId")
                .GetString()!,

            body
                .GetProperty("familyId")
                .GetString()!,

            adultEmail,

            adultPassword,

            childPin);
    }

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string FamilyId,
        string AdultEmail,
        string AdultPassword,
        string ChildPin);
}