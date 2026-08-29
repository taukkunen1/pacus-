using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class StoreHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public StoreHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task GetItems_ShouldBeAccessibleByAdultAndChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var adultResponse =
            await client.GetAsync("/api/v1/store/items");

        Assert.Equal(
            HttpStatusCode.OK,
            adultResponse.StatusCode);

        await LoginChildAsync(client, family);

        var childResponse =
            await client.GetAsync("/api/v1/store/items");

        Assert.Equal(
            HttpStatusCode.OK,
            childResponse.StatusCode);
    }

    [Fact]
    public async Task CreateItem_ShouldBeAllowedForAdult()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/store/items",
            new
            {
                title = "Hot Wheels",
                description = "Carrinho",
                cost = 3,
                category = "toy",
                icon = "car",
                stock = 1
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var item =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Hot Wheels",
            item.GetProperty("title").GetString());

        Assert.Equal(
            3,
            item.GetProperty("cost").GetInt32());

        Assert.Equal(
            true,
            item.GetProperty("active").GetBoolean());

        Assert.Equal(
            1,
            item.GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task CreateItem_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/store/items",
            new
            {
                title = "Item proibido",
                description = "teste",
                cost = 1,
                category = "toy",
                icon = "toy",
                stock = 1
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task RequestRedemption_ShouldCreatePendingRedemption()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Recompensa",
                1,
                1);

        await LoginChildAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/store/redemptions",
            new
            {
                storeItemId = itemId
            });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var redemption =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            itemId,
            redemption.GetProperty("storeItemId").GetString());

        Assert.Equal(
            "Pending",
            redemption.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RequestRedemption_WithInvalidStoreItemId_ShouldReturnBadRequest()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginChildAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/store/redemptions",
            new
            {
                storeItemId = "nao-e-object-id"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task ApproveRedemption_ShouldApproveAndConsumeFiniteStock()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskResponse = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = "Ganhar ponto para resgate",
                description = "Preparação do teste de resgate",
                type = "mandatory",
                period = "morning",
                points = 1
            });

        var taskBody =
            await taskResponse.Content.ReadAsStringAsync();

        Assert.True(
            taskResponse.StatusCode == HttpStatusCode.OK,
            $"Criação da tarefa falhou. HTTP {(int)taskResponse.StatusCode} {taskResponse.StatusCode}. Body: {taskBody}");

        var taskRoutine =
            JsonSerializer.Deserialize<JsonElement>(taskBody);

        var taskId =
            taskRoutine.GetProperty("tasks")
                .EnumerateArray()
                .Last(t =>
                    t.GetProperty("title").GetString() ==
                    "Ganhar ponto para resgate")
                .GetProperty("id")
                .GetString()!;

        var completeResponse =
            await client.PostAsync(
                $"/api/v1/daily-tasks/{taskId}/complete",
                content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            completeResponse.StatusCode);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Recompensa aprovada",
                1,
                1);

        await LoginChildAsync(client, family);

        var requestResponse =
            await client.PostAsJsonAsync(
                "/api/v1/store/redemptions",
                new
                {
                    storeItemId = itemId
                });

        Assert.Equal(
            HttpStatusCode.OK,
            requestResponse.StatusCode);

        var redemption =
            await requestResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var redemptionId =
            redemption.GetProperty("id").GetString()!;

        await LoginAdultAsync(client, family);

        var approveResponse =
            await client.PutAsync(
                $"/api/v1/store/redemptions/{redemptionId}/approve",
                content: null);

        var approveBody =
            await approveResponse.Content.ReadAsStringAsync();

        Assert.True(
            approveResponse.StatusCode == HttpStatusCode.OK,
            $"Aprovação falhou. HTTP {(int)approveResponse.StatusCode} {approveResponse.StatusCode}. Body: {approveBody}");

        var approved =
            JsonSerializer.Deserialize<JsonElement>(approveBody);

        Assert.Equal(
            "Approved",
            approved.GetProperty("status").GetString());

        var storeResponse =
            await client.GetAsync("/api/v1/store/items");

        Assert.Equal(
            HttpStatusCode.OK,
            storeResponse.StatusCode);

        var items =
            await storeResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.DoesNotContain(
            items.EnumerateArray(),
            item =>
                item.GetProperty("id").GetString() == itemId);
    }

    [Fact]
    public async Task ApproveRedemption_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Recompensa",
                1,
                2);

        await LoginChildAsync(client, family);

        var requestResponse =
            await client.PostAsJsonAsync(
                "/api/v1/store/redemptions",
                new
                {
                    storeItemId = itemId
                });

        var redemption =
            await requestResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var redemptionId =
            redemption.GetProperty("id").GetString()!;

        var approveResponse =
            await client.PutAsync(
                $"/api/v1/store/redemptions/{redemptionId}/approve",
                content: null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            approveResponse.StatusCode);
    }

    [Fact]
    public async Task RejectRedemption_ShouldRejectPendingRedemption()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Recompensa rejeitada",
                1,
                null);

        await LoginChildAsync(client, family);

        var requestResponse =
            await client.PostAsJsonAsync(
                "/api/v1/store/redemptions",
                new
                {
                    storeItemId = itemId
                });

        var redemption =
            await requestResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var redemptionId =
            redemption.GetProperty("id").GetString()!;

        await LoginAdultAsync(client, family);

        var rejectResponse =
            await client.PutAsync(
                $"/api/v1/store/redemptions/{redemptionId}/reject",
                content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            rejectResponse.StatusCode);

        var rejected =
            await rejectResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Rejected",
            rejected.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RejectRedemption_ShouldBeForbiddenForChild()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Recompensa",
                1,
                null);

        await LoginChildAsync(client, family);

        var requestResponse =
            await client.PostAsJsonAsync(
                "/api/v1/store/redemptions",
                new
                {
                    storeItemId = itemId
                });

        var redemption =
            await requestResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var redemptionId =
            redemption.GetProperty("id").GetString()!;

        var rejectResponse =
            await client.PutAsync(
                $"/api/v1/store/redemptions/{redemptionId}/reject",
                content: null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            rejectResponse.StatusCode);
    }

    [Fact]
    public async Task RequestRedemption_WhenStockIsZero_ShouldReturnBadRequest()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);

        var itemId =
            await CreateStoreItemAsync(
                client,
                "Item sem estoque",
                1,
                0);

        await LoginChildAsync(client, family);

        var response =
            await client.PostAsJsonAsync(
                "/api/v1/store/redemptions",
                new
                {
                    storeItemId = itemId
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // Isolamento por familia (checklist de seguranca, item A2) -- Familia B
    // nao pode aprovar/rejeitar um resgate que pertence a Familia A so por
    // saber o ObjectId. StoreService.GetOwnedPendingRedemptionAsync ja faz
    // essa checagem (redemption.UserId != familyId), este teste so prova
    // a garantia via HTTP e evita regressao.
    [Fact]
    public async Task ApproveRedemption_FromAnotherFamily_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        var itemId = await CreateStoreItemAsync(client, "Recompensa da Familia A", 1, 2);

        await LoginChildAsync(client, familyA);

        var requestResponse = await client.PostAsJsonAsync(
            "/api/v1/store/redemptions",
            new { storeItemId = itemId });

        var redemption = await requestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var redemptionId = redemption.GetProperty("id").GetString()!;

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var approveResponse = await client.PutAsync(
            $"/api/v1/store/redemptions/{redemptionId}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, approveResponse.StatusCode);

        // Confirma que o resgate da Familia A continua pendente (nao foi mexido).
        await LoginAdultAsync(client, familyA);

        var rejectByOwnerResponse = await client.PutAsync(
            $"/api/v1/store/redemptions/{redemptionId}/reject",
            content: null);

        Assert.Equal(HttpStatusCode.OK, rejectByOwnerResponse.StatusCode);
    }

    [Fact]
    public async Task RejectRedemption_FromAnotherFamily_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);

        var itemId = await CreateStoreItemAsync(client, "Outra recompensa da Familia A", 1, null);

        await LoginChildAsync(client, familyA);

        var requestResponse = await client.PostAsJsonAsync(
            "/api/v1/store/redemptions",
            new { storeItemId = itemId });

        var redemption = await requestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var redemptionId = redemption.GetProperty("id").GetString()!;

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var rejectResponse = await client.PutAsync(
            $"/api/v1/store/redemptions/{redemptionId}/reject",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);
    }

    [Fact]
    public async Task GetItems_ShouldNotReturnAnotherFamilysItems()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);
        var itemIdA = await CreateStoreItemAsync(client, "Item exclusivo da Familia A", 5, null);

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);

        var response = await client.GetAsync("/api/v1/store/items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.DoesNotContain(
            items.EnumerateArray(),
            i => i.GetProperty("id").GetString() == itemIdA);
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

        await response.Content.ReadAsStringAsync();
    }
    private async Task<string> CreateStoreItemAsync(
        HttpClient client,
        string title,
        int cost,
        int? stock)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/v1/store/items",
                new
                {
                    title,
                    description = "Item criado pelo teste",
                    cost,
                    category = "toy",
                    icon = "gift",
                    stock
                });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var item =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        return item
            .GetProperty("id")
            .GetString()!;
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

    private static async Task<TestFamily> BootstrapAsync(
        HttpClient client)
    {
        var suffix =
            Guid.NewGuid()
                .ToString("N")[..8];

        var adultEmail =
            $"adult-{suffix}@test.local";

        const string adultPassword = "Teste123!";
        const string childPin = "1234";

        var response =
            await client.PostAsJsonAsync(
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

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string FamilyId,
        string AdultEmail,
        string AdultPassword,
        string ChildPin);
}






