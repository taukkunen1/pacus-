using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

// Cobertura do TasksController (templates permanentes de tarefa), com foco em
// isolamento por familia — auditoria de seguranca encontrou Delete() chamando o
// repositorio direto, sem checar FamilyId (corrigido para passar por
// TaskTemplateService.DeleteAsync, que verifica template.FamilyId == familyId).
public class TasksHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public TasksHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Delete_OtherFamilysTemplate_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);

        using var clientA = factory.CreateClient();
        var familyA = await BootstrapAsync(clientA);
        await LoginAdultAsync(clientA, familyA);
        var templateIdFromFamilyA = await CreateTemplateAndGetIdAsync(clientA);

        using var clientB = factory.CreateClient();
        var familyB = await BootstrapAsync(clientB);
        await LoginAdultAsync(clientB, familyB);

        // Familia B, autenticada com o proprio token, tenta excluir o template que
        // pertence a Familia A so por saber o ObjectId.
        var deleteResponse = await clientB.DeleteAsync(
            $"/api/v1/tasks/{templateIdFromFamilyA}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        // Confirma que o template da Familia A continua ativo/intacto.
        var listResponse = await clientA.GetAsync("/api/v1/tasks");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var templates = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            templates.EnumerateArray(),
            t => t.GetProperty("id").GetString() == templateIdFromFamilyA);
    }

    [Fact]
    public async Task Update_OtherFamilysTemplate_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);

        using var clientA = factory.CreateClient();
        var familyA = await BootstrapAsync(clientA);
        await LoginAdultAsync(clientA, familyA);
        var templateIdFromFamilyA = await CreateTemplateAndGetIdAsync(clientA);

        using var clientB = factory.CreateClient();
        var familyB = await BootstrapAsync(clientB);
        await LoginAdultAsync(clientB, familyB);

        var response = await clientB.PutAsJsonAsync(
            $"/api/v1/tasks/{templateIdFromFamilyA}",
            new
            {
                title = "Sequestrada pela Familia B",
                description = (string?)null,
                type = "mandatory",
                period = "morning",
                points = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnTemplate_ShouldSucceed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        var templateId = await CreateTemplateAndGetIdAsync(client);

        var response = await client.DeleteAsync($"/api/v1/tasks/{templateId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await client.GetAsync("/api/v1/tasks");
        var templates = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.DoesNotContain(
            templates.EnumerateArray(),
            t => t.GetProperty("id").GetString() == templateId);
    }

    [Fact]
    public async Task Delete_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/tasks/{MongoDB.Bson.ObjectId.GenerateNewId()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsChild_ShouldReturnForbidden()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        var templateId = await CreateTemplateAndGetIdAsync(client);

        await LoginChildAsync(client, family);

        var response = await client.DeleteAsync($"/api/v1/tasks/{templateId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> CreateTemplateAndGetIdAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = $"Template-{Guid.NewGuid():N}",
                description = "Template criado para o teste",
                type = "mandatory",
                period = "morning",
                points = 2
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var template = await response.Content.ReadFromJsonAsync<JsonElement>();

        return template.GetProperty("id").GetString()!;
    }

    private async Task LoginAdultAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new
            {
                email = family.AdultEmail,
                password = family.AdultPassword
            });

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
            new
            {
                userId = family.ChildUserId,
                pin = family.ChildPin
            });

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

        var adultName = $"Adulto {suffix}";
        var adultEmail = $"adult-{suffix}@test.local";
        var adultPassword = "Teste123!";
        var childName = $"Crianca {suffix}";
        var childPin = "1234";

        var response = await client.PostAsJsonAsync(
            "/api/v1/bootstrap",
            new
            {
                adultName,
                adultEmail,
                adultPassword,
                childName,
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
