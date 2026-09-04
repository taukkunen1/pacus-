using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

// Cobre o sistema de cadastro/login por codigo de familia (ver User.FamilyCode):
// o bootstrap devolvendo o codigo, a busca anonima de criancas por codigo (usada
// pela tela de login da crianca em vez de um id do Mongo colado) e a
// reconsulta do codigo pelo adulto. Isolamento por familia (checklist de
// seguranca, item A2) tambem coberto -- o codigo de uma familia nunca deve
// devolver criancas de outra.
public class FamilyCodeHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public FamilyCodeHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Bootstrap_ShouldReturnFamilyCodeInExpectedFormat()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        Assert.Matches("^[A-Z2-9]{3}-[A-Z2-9]{3}$", family.FamilyCode);
    }

    [Fact]
    public async Task GetChildrenByCode_WithValidCode_ReturnsChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        var response = await client.GetAsync($"/api/v1/family/by-code/{family.FamilyCode}/children");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var children = body.EnumerateArray().ToList();

        Assert.Single(children);
        Assert.Equal(family.ChildUserId, children[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetChildrenByCode_IsCaseInsensitiveAndIgnoresDash()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        var noDashLower = family.FamilyCode.Replace("-", "").ToLowerInvariant();

        var response = await client.GetAsync($"/api/v1/family/by-code/{noDashLower}/children");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetArrayLength());
    }

    [Fact]
    public async Task GetChildrenByCode_WithUnknownCode_ReturnsEmptyList()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/family/by-code/ZZZ-ZZZ/children");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task GetChildrenByCode_WithMalformedCode_ReturnsEmptyListInsteadOfError()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/family/by-code/nao-e-um-codigo-valido/children");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task GetChildrenByCode_NeverExposesOtherFamilysChildren()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        var familyB = await BootstrapAsync(client);

        var response = await client.GetAsync($"/api/v1/family/by-code/{familyA.FamilyCode}/children");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var children = body.EnumerateArray().ToList();

        Assert.Single(children);
        Assert.Equal(familyA.ChildUserId, children[0].GetProperty("id").GetString());
        Assert.NotEqual(familyB.ChildUserId, children[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetFamilyCode_ShouldBeAllowedForAdult_AndMatchBootstrapValue()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.GetAsync("/api/v1/family/code");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(family.FamilyCode, body.GetProperty("familyCode").GetString());
    }

    [Fact]
    public async Task GetFamilyCode_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.GetAsync("/api/v1/family/code");
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
