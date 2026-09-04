using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;

namespace Pacus.IntegrationTests;

// Cobertura do endpoint de exclusao de conta (checklist de seguranca e LGPD,
// item B3). Restrito ao adulto e exige a senha atual; verifica que apaga os
// dados de toda a familia (hard delete), anonimiza (em vez de apagar) os logs
// de auditoria, nao vaza nem afeta outras familias, e exige senha correta.
public class AccountHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public AccountHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task DeleteAccount_ShouldEraseFamilyData_WhenPasswordIsCorrect()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Tarefa da familia a ser excluida",
                description = (string?)null,
                type = "expected",
                period = "afternoon",
                points = 2
            });

        var response = await client.SendAsync(BuildDeleteRequest(family.AdultPassword));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // A conta nao existe mais -- login subsequente deve falhar.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new { email = family.AdultEmail, password = family.AdultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(factory.DatabaseName);
        var familyIdFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
            "familyId", MongoDB.Bson.ObjectId.Parse(family.FamilyId));

        foreach (var collectionName in new[]
        {
            "users", "pacus", "habitats", "settings", "daily_routines",
            "task_templates", "point_transactions", "store_items", "redemptions",
        })
        {
            var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
            var count = await collection.CountDocumentsAsync(familyIdFilter);
            Assert.Equal(0, count);
        }

        // pacus_growth e task_events guardam o FamilyId no campo "userId" (nao
        // renomeado no A4 -- ver docs/DATA_MAP.md), entao o filtro e diferente.
        var userIdFamilyFilter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
            "userId", MongoDB.Bson.ObjectId.Parse(family.FamilyId));

        foreach (var collectionName in new[] { "pacus_growth", "task_events" })
        {
            var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
            var count = await collection.CountDocumentsAsync(userIdFamilyFilter);
            Assert.Equal(0, count);
        }
    }

    // audit_logs e a excecao da regra de hard delete: preservado, mas com o
    // vinculo com a pessoa removido (ActorId) e uma data de purga marcada.
    [Fact]
    public async Task DeleteAccount_ShouldAnonymizeAuditLogs_NotDeleteThem()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var templateResponse = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Tarefa para gerar log de auditoria",
                description = (string?)null,
                type = "expected",
                period = "afternoon",
                points = 2
            });
        var template = await templateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = template.GetProperty("id").GetString();

        await client.DeleteAsync($"/api/v1/tasks/{templateId}"); // gera audit log (soft delete)

        var response = await client.SendAsync(BuildDeleteRequest(family.AdultPassword));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(factory.DatabaseName);
        var auditLogs = database.GetCollection<Pacus.Domain.Entities.AuditLog>("audit_logs");

        var logs = await auditLogs
            .Find(a => a.FamilyId == MongoDB.Bson.ObjectId.Parse(family.FamilyId))
            .ToListAsync();

        Assert.NotEmpty(logs);
        Assert.All(logs, log =>
        {
            Assert.True(log.Anonymized);
            Assert.Equal(MongoDB.Bson.ObjectId.Empty, log.ActorId);
            Assert.NotNull(log.PurgeAt);
        });

        // A propria exclusao de conta gera uma entrada, ja anonimizada.
        Assert.Contains(logs, l => l.Action == "account.deleted");
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturnUnauthorized_WhenPasswordIsWrong()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.SendAsync(BuildDeleteRequest("SenhaErrada123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Conta nao foi tocada -- login continua funcionando.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new { email = family.AdultEmail, password = family.AdultPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.SendAsync(BuildDeleteRequest(family.AdultPassword));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(BuildDeleteRequest("qualquer-senha"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Isolamento por familia: excluir a conta da Familia A nunca pode afetar a
    // Familia B.
    [Fact]
    public async Task DeleteAccount_ShouldNotAffectOtherFamilies()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);
        await client.SendAsync(BuildDeleteRequest(familyA.AdultPassword));

        client.DefaultRequestHeaders.Authorization = null;

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var response = await client.GetAsync("/api/v1/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(familyB.FamilyId, json.GetProperty("familyId").GetString());
    }

    private static HttpRequestMessage BuildDeleteRequest(string password) =>
        new(HttpMethod.Delete, "/api/v1/account")
        {
            Content = JsonContent.Create(new { password }),
        };

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
