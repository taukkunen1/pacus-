using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;

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

    // Recorrencia "weekday_rotation" (ex.: Momento Criativo, uma atividade diferente
    // por dia util) e nova nesta mudanca -- este teste confere o contrato HTTP de
    // ponta a ponta: nomes de dia da semana em ingles minusculo na ida (o
    // JsonStringEnumConverter do Program.cs usa CamelCase) precisam ser aceitos pelo
    // Enum.TryParse(ignoreCase:true) do TaskTemplateService, e o retorno inclui
    // recurrence + variants (a logica de materializar por dia fica coberta nos
    // testes unitarios de DailyRoutineService, que conseguem controlar a data --
    // aqui so valida o formato JSON de fato trafegado pela API).
    [Fact]
    public async Task Create_WeekdayRotation_ShouldPersistRecurrenceAndVariants()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Momento Criativo",
                description = (string?)null,
                type = "challenge",
                period = "afternoon",
                points = 3,
                recurrence = "weekday_rotation",
                variants = new[]
                {
                    new { dayOfWeek = "monday", title = "Missão Detetive", description = (string?)null },
                    new { dayOfWeek = "tuesday", title = "Desafio Engenheiro", description = (string?)null },
                    new { dayOfWeek = "wednesday", title = "Chef por um Dia", description = (string?)null },
                    new { dayOfWeek = "thursday", title = "Inventor Maluco", description = (string?)null },
                    new { dayOfWeek = "friday", title = "Missão 20 Minutos", description = (string?)null },
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("weekday_rotation", body.GetProperty("recurrence").GetString());

        var variants = body.GetProperty("variants").EnumerateArray().ToList();
        Assert.Equal(5, variants.Count);
        Assert.Equal("monday", variants[0].GetProperty("dayOfWeek").GetString());
        Assert.Equal("Missão Detetive", variants[0].GetProperty("title").GetString());
    }

    // Points por variante (override do valor do template) e novo nesta mudanca --
    // pedido do usuario pra poder valer mais em missoes que exigem supervisao de
    // adulto. So testa o contrato JSON (a resolucao null-usa-o-do-template ja fica
    // coberta em unit tests, que conseguem controlar a data materializada).
    [Fact]
    public async Task Create_WeekdayRotation_VariantWithOwnPoints_ShouldOverrideTemplatePoints()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Momento Criativo",
                description = (string?)null,
                type = "challenge",
                period = "afternoon",
                points = 3,
                recurrence = "weekday_rotation",
                variants = new object[]
                {
                    new { dayOfWeek = "monday", title = "Missão Detetive", description = (string?)null },
                    new { dayOfWeek = "wednesday", title = "Chef por um Dia", description = (string?)null, points = 5 },
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var variants = body.GetProperty("variants").EnumerateArray().ToList();

        var monday = variants.Single(v => v.GetProperty("dayOfWeek").GetString() == "monday");
        Assert.False(monday.TryGetProperty("points", out var mondayPoints) && mondayPoints.ValueKind != JsonValueKind.Null);

        var wednesday = variants.Single(v => v.GetProperty("dayOfWeek").GetString() == "wednesday");
        Assert.Equal(5, wednesday.GetProperty("points").GetInt32());
    }

    [Fact]
    public async Task Create_WeekdayRotation_WithSaturdayVariant_ShouldBeRejected()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Momento Criativo",
                description = (string?)null,
                type = "challenge",
                period = "afternoon",
                points = 3,
                recurrence = "weekday_rotation",
                variants = new[]
                {
                    new { dayOfWeek = "saturday", title = "Passeio em família", description = (string?)null },
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Recorrencia "custom" (dias especificos, mesmo conteudo em todos -- ex.:
    // "Inglês" so terca e quarta, "Escoteiro" so sabado) e nova nesta mudanca.
    // Mesmo proposito do teste de weekday_rotation acima: validar o contrato JSON
    // (nomes de dia em ingles minusculo) de ponta a ponta via HTTP real.
    [Fact]
    public async Task Create_CustomRecurrence_ShouldPersistCustomDays()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Inglês",
                description = (string?)null,
                type = "challenge",
                period = "afternoon",
                points = 3,
                recurrence = "custom",
                customDays = new[] { "tuesday", "wednesday" }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("custom", body.GetProperty("recurrence").GetString());

        var customDays = body.GetProperty("customDays").EnumerateArray()
            .Select(d => d.GetString())
            .ToList();
        Assert.Equal(new[] { "tuesday", "wednesday" }, customDays);
    }

    [Fact]
    public async Task Create_CustomRecurrence_WithoutDays_ShouldBeRejected()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new
            {
                title = "Escoteiro",
                description = (string?)null,
                type = "expected",
                period = "morning",
                points = 2,
                recurrence = "custom"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

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

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    // Log de auditoria (checklist de seguranca, item A5): excluir uma tarefa
    // permanente e uma acao administrativa sensivel e precisa deixar rastro
    // na colecao audit_logs, separado do dado em si (o template so fica com
    // DeletedAt marcado -- soft delete).
    [Fact]
    public async Task Delete_ShouldCreateAuditLogEntry()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        var templateId = await CreateTemplateAndGetIdAsync(client);

        var response = await client.DeleteAsync($"/api/v1/tasks/{templateId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(factory.DatabaseName);
        var auditLogs = database.GetCollection<Pacus.Domain.Entities.AuditLog>("audit_logs");

        var log = await auditLogs
            .Find(a => a.Action == "task_template.deleted" && a.EntityId == templateId)
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Equal(family.FamilyId, log.FamilyId.ToString());
        Assert.Equal(family.AdultUserId, log.ActorId.ToString());
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
