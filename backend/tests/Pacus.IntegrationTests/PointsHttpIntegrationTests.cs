using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.IntegrationTests;

public sealed class PointsHttpIntegrationTests
    : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public PointsHttpIntegrationTests(
        MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task GetBalance_WhenNoTransactions_ShouldReturnZero()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var response =
            await client.GetAsync("/api/v1/points");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            body.GetProperty("balance").GetInt32());

        Assert.Equal(
            0,
            body.GetProperty("brl").GetDouble());
    }

    [Fact]
    public async Task GetBalance_ShouldSumAllPointTransactions()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        await InsertTransactionAsync(
            factory,
            family.FamilyId,
            100,
            "Tarefa concluída");

        await InsertTransactionAsync(
            factory,
            family.FamilyId,
            -30,
            "Resgate na loja");

        await InsertTransactionAsync(
            factory,
            family.FamilyId,
            20,
            "Bônus");

        await LoginAdultAsync(client, family);

        var response =
            await client.GetAsync("/api/v1/points");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            90,
            body.GetProperty("balance").GetInt32());

        // 90 * Settings.DefaultPointToBrlRate (0.06, sem Settings salvo pra familia) = 5.4.
        // Era 4.5 com a taxa antiga (0.05), que por coincidencia batia exato em double.
        // 0.06 nao bate: 90 * 0.06 == 5.3999999999999995 em IEEE754, nao 5.4 exato --
        // por isso a comparacao com precisao (ver overload Assert.Equal(double,double,int))
        // em vez de igualdade exata, que e a forma certa de comparar double de qualquer jeito.
        Assert.Equal(
            5.4,
            body.GetProperty("brl").GetDouble(),
            precision: 10);
    }

    // Cura da taxa antiga (0.05) congelada num documento de Settings criado antes da
    // mudanca pra 0.06 -- ver comentario em PointsController.GetPointToBrlRateAsync.
    // Simula esse estado inserindo o documento direto no Mongo (nao existe endpoint
    // pra escolher essa taxa) e confere que o saldo em R$ ja vem com 0.06 na primeira
    // leitura, alem do documento no banco ser corrigido (nao so o calculo em memoria).
    [Fact]
    public async Task GetBalance_WithLegacySettingsRate_ShouldSelfHealToCurrentDefault()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await InsertTransactionAsync(
            factory,
            family.FamilyId,
            100,
            "Tarefa concluída");

        var mongoClient = new MongoClient(GetConnectionString(factory));
        var database = mongoClient.GetDatabase(factory.DatabaseName);
        var settingsCollection = database.GetCollection<Settings>("settings");

        var familyObjectId = ObjectId.Parse(family.FamilyId);

        await settingsCollection.InsertOneAsync(new Settings
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyObjectId,
            PointToBrlRate = Settings.LegacyDefaultPointToBrlRate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await LoginAdultAsync(client, family);

        var response = await client.GetAsync("/api/v1/points");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // 100 * 0.06 = 6. Se a cura nao funcionasse viria 5 (100 * 0.05, taxa antiga).
        Assert.Equal(6.0, body.GetProperty("brl").GetDouble(), precision: 10);

        var storedSettings = await settingsCollection
            .Find(s => s.FamilyId == familyObjectId)
            .FirstOrDefaultAsync();

        Assert.NotNull(storedSettings);
        Assert.Equal(Settings.DefaultPointToBrlRate, storedSettings.PointToBrlRate, precision: 10);
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnOnlyCurrentFamilyTransactions()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var family =
            await BootstrapAsync(client);

        var otherFamily =
            await BootstrapAsync(client);

        await InsertTransactionAsync(
            factory,
            family.FamilyId,
            50,
            "Transação da família atual");

        await InsertTransactionAsync(
            factory,
            otherFamily.FamilyId,
            999,
            "Transação de outra família");

        await LoginAdultAsync(client, family);

        var response =
            await client.GetAsync(
                "/api/v1/points/transactions");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Endpoint paginado (achado #4 da auditoria de API de 2026-09-01 -- ver
        // docs/ESTADO_ATUAL.md): a resposta agora e um PagedResult, nao mais um
        // array solto.
        var transactions =
            body.GetProperty("items");

        Assert.Equal(
            JsonValueKind.Array,
            transactions.ValueKind);

        Assert.Single(
            transactions.EnumerateArray());

        Assert.Equal(
            1,
            body.GetProperty("totalCount").GetInt64());

        var transaction =
            transactions[0];

        Assert.Equal(
            "Transação da família atual",
            transaction.GetProperty("taskTitle").GetString());

        Assert.Equal(
            50,
            transaction.GetProperty("points").GetInt32());
    }

    [Fact]
    public async Task PointsEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory =
            new PacusApiFactory(_mongo.ConnectionString);

        using var client =
            factory.CreateClient();

        var response =
            await client.GetAsync("/api/v1/points");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private static async Task InsertTransactionAsync(
        PacusApiFactory factory,
        string familyId,
        int points,
        string taskTitle)
    {
        var client =
            new MongoClient(
                GetConnectionString(factory));

        var database =
            client.GetDatabase(
                factory.DatabaseName);

        var collection =
    database.GetCollection<PointTransaction>(
        "point_transactions");

        var familyObjectId =
            ObjectId.Parse(familyId);

        var transaction =
            new PointTransaction
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = familyObjectId,
                Date = "2026-08-24",
                TaskId = ObjectId.GenerateNewId().ToString(),
                TaskTitle = taskTitle,
                Type = PointTransactionType.Award,
                Points = points,
                BalanceAfter = points,
                ActorId = familyObjectId,
                ActorRole = UserRole.Adult,
                CreatedAt = DateTime.UtcNow
            };

        await collection.InsertOneAsync(transaction);
    }

    private static string GetConnectionString(
        PacusApiFactory factory)
    {
        var field =
            typeof(PacusApiFactory)
                .GetField(
                    "_connectionString",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

        return (string)field!.GetValue(factory)!;
    }

    // Troca de papel (checklist de seguranca, item A3): crianca tentando
    // ajustar o saldo manualmente, acao restrita ao adulto via
    // [RequireRole(UserRole.Adult)].
    [Fact]
    public async Task AdjustBalance_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/points/adjust",
            new { balance = 999, reason = "crianca tentando ajustar saldo" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Log de auditoria (checklist de seguranca, item A5): ajuste manual de
    // saldo e uma acao administrativa sensivel e precisa deixar rastro na
    // colecao audit_logs, separado da propria transacao em point_transactions.
    [Fact]
    public async Task AdjustBalance_ShouldCreateAuditLogEntry()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/points/adjust",
            new { balance = 42, reason = "teste de auditoria" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mongoClient = new MongoClient(GetConnectionString(factory));
        var database = mongoClient.GetDatabase(factory.DatabaseName);
        var auditLogs = database.GetCollection<Pacus.Domain.Entities.AuditLog>("audit_logs");

        var log = await auditLogs
            .Find(a => a.Action == "points.manual_adjustment" && a.FamilyId == ObjectId.Parse(family.FamilyId))
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Contains("teste de auditoria", log.Details);
    }

    private async Task LoginChildAsync(
        HttpClient client,
        TestFamily family)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/v1/auth/child/login",
                new
                {
                    userId = family.ChildUserId,
                    pin = family.ChildPin
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        var token =
            body.GetProperty("token").GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);
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
            body.GetProperty("token").GetString();

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
                    childPin
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
            body.GetProperty("adultUserId")
                .GetString()!,
            body.GetProperty("childUserId")
                .GetString()!,
            body.GetProperty("familyId")
                .GetString()!,
            adultEmail,
            adultPassword,
            childPin);
    }

    // Isolamento por familia (checklist de seguranca, item A2). Saldo e
    // extrato sao sempre "os da familia do token" (sem id de rota), entao a
    // garantia e estrutural -- este teste prova via HTTP que ajustar o saldo
    // da Familia A nao vaza pro saldo da Familia B.
    [Fact]
    public async Task Balance_ShouldBeIsolatedPerFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        var adjustA = await client.PostAsJsonAsync(
            "/api/v1/points/adjust",
            new { balance = 50, reason = "saldo inicial familia A" });

        Assert.Equal(HttpStatusCode.OK, adjustA.StatusCode);

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var balanceB = await client.GetAsync("/api/v1/points");
        Assert.Equal(HttpStatusCode.OK, balanceB.StatusCode);

        var body = await balanceB.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("balance").GetInt32());
    }

    [Fact]
    public async Task Transactions_ShouldNotLeakBetweenFamilies()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        await client.PostAsJsonAsync(
            "/api/v1/points/adjust",
            new { balance = 7, reason = "transacao exclusiva da familia A" });

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var response = await client.GetAsync("/api/v1/points/transactions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var transactions = body.GetProperty("items");

        Assert.DoesNotContain(
            transactions.EnumerateArray(),
            t => t.TryGetProperty("reason", out var reason)
                 && reason.GetString() == "transacao exclusiva da familia A");
    }

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string FamilyId,
        string AdultEmail,
        string AdultPassword,
        string ChildPin);
}