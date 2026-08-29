using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

// Nao existia cobertura para PacusController ainda (achado durante o
// checklist de seguranca, item A3 -- troca de papel / manipulacao de
// ObjectId). UpdateState e restrito ao adulto via [RequireRole(UserRole.Adult)]
// e sempre opera sobre "o PACUS da familia do token", sem id de rota.
public class PacusHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public PacusHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task UpdateState_ShouldBeAllowedForAdult()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/state",
            new { stage = "Young", size = 2.5, totalClosedDays = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Young", body.GetProperty("stage").GetString());
        Assert.Equal(10, body.GetProperty("totalClosedDays").GetInt32());
    }

    // Troca de papel (checklist de seguranca, item A3): crianca tentando
    // corrigir manualmente o estagio/tamanho do PACUS, acao restrita ao
    // adulto (usada pra migrar progresso de uma versao anterior do app).
    [Fact]
    public async Task UpdateState_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/state",
            new { stage = "Adult", size = 99, totalClosedDays = 9999 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Isolamento por familia (checklist de seguranca, item A2/A3). Sem id de
    // rota -- e sempre "o PACUS da familia do token" -- garantia estrutural
    // (GetByFamilyIdAsync so busca pelo FamilyId do token), provada aqui via
    // HTTP.
    [Fact]
    public async Task State_ShouldBeIsolatedPerFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        var updateA = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/state",
            new { stage = "Adult", size = 50, totalClosedDays = 777 });

        Assert.Equal(HttpStatusCode.OK, updateA.StatusCode);

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var response = await client.GetAsync("/api/v1/pacus/me/state");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Familia B nunca chegou perto disso -- se estivesse vendo o PACUS
        // da Familia A, totalClosedDays viria 777.
        Assert.NotEqual(777, body.GetProperty("totalClosedDays").GetInt32());
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
                childPin
            });

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Created });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new TestFamily(
            body.GetProperty("adultUserId").GetString()!,
            body.GetProperty("childUserId").GetString()!,
            body.GetProperty("familyId").GetString()!,
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
