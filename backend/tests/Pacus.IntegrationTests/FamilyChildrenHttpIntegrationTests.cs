using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

// Cobre POST /family/children (FamilyController.CreateChild) -- o unico jeito de
// uma familia ter uma crianca antes deste endpoint era o bootstrap (1 adulto + 1
// crianca juntos, ver BootstrapHttpIntegrationTests se existir, ou BootstrapService),
// entao uma familia que ficasse sem nenhuma crianca cadastrada (ex.: exclusao manual
// de um registro no banco, caso real que motivou este recurso) nao tinha como se
// recuperar pelo app.
public class FamilyChildrenHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public FamilyChildrenHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task CreateChild_AsAdult_AddsChildToTheFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/family/children",
            new { name = "Segunda Crianca", pin = "5678" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var childId = body.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(childId));
        Assert.Equal("Segunda Crianca", body.GetProperty("name").GetString());

        // Aparece na lista autenticada de criancas da familia (usada pela tela de
        // "Trocar PIN") junto com a que ja existia do bootstrap.
        var childrenResponse = await client.GetAsync("/api/v1/family/children");
        var children = (await childrenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Select(c => c.GetProperty("id").GetString())
            .ToList();

        Assert.Contains(childId, children);
        Assert.Contains(family.ChildUserId, children);

        // Consegue logar de verdade com o PIN informado na criacao.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new { userId = childId, pin = "5678" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task CreateChild_ForFamilyWithoutAFamilyCodeYet_GeneratesOneAndSharesIt()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        // Simula uma familia legada (ver FamilyCodeHttpIntegrationTests) que nunca
        // tinha um FamilyCode -- CreateChild tem o mesmo backfill de GetFamilyCode.
        var users = new MongoDB.Driver.MongoClient(_mongo.ConnectionString)
            .GetDatabase(factory.DatabaseName)
            .GetCollection<Pacus.Domain.Entities.User>("users");
        await users.UpdateManyAsync(
            MongoDB.Driver.Builders<Pacus.Domain.Entities.User>.Filter.Eq(u => u.FamilyId, MongoDB.Bson.ObjectId.Parse(family.FamilyId)),
            MongoDB.Driver.Builders<Pacus.Domain.Entities.User>.Update.Set(u => u.FamilyCode, string.Empty));

        var response = await client.PostAsJsonAsync(
            "/api/v1/family/children",
            new { name = "Terceira Crianca", pin = "1111" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var codeResponse = await client.GetAsync("/api/v1/family/code");
        var newCode = (await codeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("familyCode")
            .GetString();

        Assert.NotNull(newCode);
        Assert.Matches("^[A-Z2-9]{3}-[A-Z2-9]{3}$", newCode);

        var childrenByCode = await client.GetAsync($"/api/v1/family/by-code/{newCode}/children");
        var names = (await childrenByCode.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("Terceira Crianca", names);
    }

    [Fact]
    public async Task CreateChild_WithInvalidPin_ReturnsBadRequest()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/family/children",
            new { name = "Crianca Invalida", pin = "12" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateChild_AsChild_IsForbidden()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/family/children",
            new { name = "Nao Deveria Existir", pin = "0000" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task LoginAdultAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new { email = family.AdultEmail, password = family.AdultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task LoginChildAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new { userId = family.ChildUserId, pin = family.ChildPin });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<TestFamily> BootstrapAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var adultEmail = $"adult-{suffix}@test.local";
        const string adultPassword = "Teste123!";
        const string childPin = "1234";

        var response = await client.PostAsJsonAsync(
            "/api/v1/bootstrap",
            new
            {
                adultName = $"Adulto {suffix}",
                adultEmail,
                adultPassword,
                childName = $"Crianca {suffix}",
                childPin,
                responsibleConsent = true
            });

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Created });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new TestFamily(
            body.GetProperty("adultUserId").GetString()!,
            body.GetProperty("childUserId").GetString()!,
            body.GetProperty("familyId").GetString()!,
            body.GetProperty("familyCode").GetString()!,
            adultEmail,
            adultPassword,
            childPin);
    }

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string FamilyId,
        string FamilyCode,
        string AdultEmail,
        string AdultPassword,
        string ChildPin);
}
