using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

// Cobertura do endpoint de exportacao de dados (checklist de LGPD, item B2 --
// portabilidade de dados). Restrito ao adulto; verifica que o export traz os
// dados certos, nao vaza entre familias, e nunca inclui hash de senha/PIN.
public class ExportHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public ExportHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Export_ShouldBeAllowedForAdult_AndIncludeFamilyData()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.GetAsync("/api/v1/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Contains("pacus-dados-", response.Content.Headers.ContentDisposition!.FileName);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(family.FamilyId, json.GetProperty("familyId").GetString());

        var members = json.GetProperty("members").EnumerateArray().ToList();
        Assert.Equal(2, members.Count); // adulto + crianca

        Assert.Contains(members, m => m.GetProperty("id").GetString() == family.AdultUserId);
        Assert.Contains(members, m => m.GetProperty("id").GetString() == family.ChildUserId);
    }

    [Fact]
    public async Task Export_ShouldNeverIncludePasswordOrPinHash()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.GetAsync("/api/v1/export");
        var rawJson = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", rawJson);
        Assert.DoesNotContain("pinHash", rawJson);
    }

    [Fact]
    public async Task Export_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.GetAsync("/api/v1/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Isolamento por familia: garante que a exportacao da Familia A nunca traz
    // nada da Familia B, mesmo indiretamente (ex. via task_templates, store_items).
    [Fact]
    public async Task Export_ShouldNotLeakDataFromAnotherFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Tarefa exclusiva da Familia A",
                description = (string?)null,
                type = "expected",
                period = "afternoon",
                points = 2
            });

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var response = await client.GetAsync("/api/v1/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(familyB.FamilyId, json.GetProperty("familyId").GetString());

        var taskTemplates = json.GetProperty("taskTemplates").EnumerateArray();
        Assert.DoesNotContain(
            taskTemplates,
            t => t.GetProperty("title").GetString() == "Tarefa exclusiva da Familia A");
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
