using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;

namespace Pacus.IntegrationTests;

public class DailyTasksHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public DailyTasksHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Create_ShouldCreateAdHocTask_ThroughHttp()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = $"Tarefa {Guid.NewGuid():N}",
                description = "Tarefa criada pelo teste HTTP",
                type = "mandatory",
                period = "morning",
                points = 2
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var routine =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var created = routine
            .GetProperty("tasks")
            .EnumerateArray()
            .Last(t =>
                t.GetProperty("title")
                    .GetString()!
                    .StartsWith("Tarefa "));

        Assert.Equal("mandatory", created.GetProperty("type").GetString());
        Assert.Equal("morning", created.GetProperty("period").GetString());
        Assert.Equal(2, created.GetProperty("points").GetInt32());
        Assert.Equal("adult", created.GetProperty("origin").GetString());
        Assert.Equal("pending", created.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_WithInvalidPoints_ShouldReturnBadRequest()
    {
        // Regra de negocio atual (DailyRoutineService.ValidatePoints) aceita
        // qualquer valor entre -10 e 10, exceto zero -- entao 5 pontos, que
        // este teste usava antes, e valido hoje. Trocado para um valor
        // realmente fora do range (11), mantendo a intencao original do
        // teste (pontos invalidos -> 400).
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = "Pontos inválidos",
                description = (string?)null,
                type = "mandatory",
                period = "morning",
                points = 11
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(
            "Cada tarefa deve valer entre 1 e 10 Pacus Points",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CompleteAndReopen_ShouldChangeTaskState_ThroughHttp()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        var completeResponse = await client.PostAsync(
            $"/api/v1/daily-tasks/{taskId}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completedRoutine =
            await completeResponse.Content.ReadFromJsonAsync<JsonElement>();

        var completedTask =
            FindTask(completedRoutine, taskId);

        Assert.Equal(
            "done",
            completedTask.GetProperty("status").GetString());

        Assert.NotEqual(
            JsonValueKind.Null,
            completedTask.GetProperty("completedAt").ValueKind);

        var reopenResponse = await client.PostAsync(
            $"/api/v1/daily-tasks/{taskId}/reopen",
            content: null);

        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var reopenedRoutine =
            await reopenResponse.Content.ReadFromJsonAsync<JsonElement>();

        var reopenedTask =
            FindTask(reopenedRoutine, taskId);

        Assert.Equal(
            "pending",
            reopenedTask.GetProperty("status").GetString());

        Assert.Equal(
            JsonValueKind.Null,
            reopenedTask.GetProperty("completedAt").ValueKind);
    }

    // Manipulacao de ObjectId (checklist de seguranca, item A3): adulto da
    // Familia B tenta completar/editar/deletar uma tarefa cujo id pertence
    // a rotina da Familia A. GetLatestOpenAsync ja escopa por FamilyId, entao
    // o id de outra familia nunca aparece na rotina de quem esta autenticado
    // -- a garantia e estrutural, este teste prova via HTTP.
    [Fact]
    public async Task TaskOperations_WithAnotherFamilysTaskId_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);
        await EnsureTodayRoutineAsync(client);

        var taskIdFromFamilyA = await CreateTaskAndGetIdAsync(client);

        var familyB = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyB);
        await EnsureTodayRoutineAsync(client);

        var completeResponse = await client.PostAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, completeResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}",
            new
            {
                title = "Sequestrada pela Familia B",
                description = (string?)null,
                type = "expected",
                period = "afternoon",
                points = 3
            });

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    // Checklist de seguranca e LGPD, item C2: a crianca tem acesso estrutural a estes
    // mesmos endpoints (complete/update/delete nao tem [RequireRole], e autonomia da
    // crianca sobre o dia atual e proposital -- ver comentarios no controller). O teste
    // acima ja cobre o adulto; este cobre a crianca tentando o mesmo ataque de id.
    [Fact]
    public async Task TaskOperations_WithAnotherFamilysTaskId_AsChild_ShouldNotBeAllowed()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var familyA = await BootstrapAsync(client);
        await LoginAdultAsync(client, familyA);
        await EnsureTodayRoutineAsync(client);

        var taskIdFromFamilyA = await CreateTaskAndGetIdAsync(client);

        var familyB = await BootstrapAsync(client);
        await LoginChildAsync(client, familyB);
        await EnsureTodayRoutineAsync(client);

        var completeResponse = await client.PostAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}/complete",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, completeResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}",
            new
            {
                title = "Sequestrada pela crianca da Familia B",
                description = (string?)null,
                type = "expected",
                period = "afternoon",
                points = 3
            });

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/daily-tasks/{taskIdFromFamilyA}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task AdjustPoints_ShouldChangeTaskPoints_ThroughHttp()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}/points",
            new
            {
                points = 3
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var routine =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var task = FindTask(routine, taskId);

        Assert.Equal(3, task.GetProperty("points").GetInt32());
    }

    [Fact]
    public async Task Update_ShouldChangeTaskData_ThroughHttp()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}",
            new
            {
                title = "Tarefa atualizada",
                description = "Descrição atualizada",
                type = "challenge",
                period = "evening",
                points = 1
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var routine =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var task = FindTask(routine, taskId);

        Assert.Equal(
            "Tarefa atualizada",
            task.GetProperty("title").GetString());

        Assert.Equal(
            "Descrição atualizada",
            task.GetProperty("description").GetString());

        Assert.Equal(
            "challenge",
            task.GetProperty("type").GetString());

        Assert.Equal(
            "evening",
            task.GetProperty("period").GetString());

        Assert.Equal(
            1,
            task.GetProperty("points").GetInt32());
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteTask_ThroughHttp()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        var response = await client.DeleteAsync(
            $"/api/v1/daily-tasks/{taskId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var routine =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var task = FindTask(routine, taskId);

        Assert.NotEqual(
            JsonValueKind.Null,
            task.GetProperty("deletedAt").ValueKind);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = "Sem autenticação",
                description = (string?)null,
                type = "mandatory",
                period = "morning",
                points = 1
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ChildPermissions_ShouldBlockCreate_WhenDisabled()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await SetChildPermissionsAsync(
            factory,
            family.FamilyId,
            canCreateTasks: false);

        await LoginChildAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = "Criação bloqueada",
                description = (string?)null,
                type = "mandatory",
                period = "morning",
                points = 1
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChildPermissions_ShouldBlockPoints_WhenDisabled()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        await SetChildPermissionsAsync(
            factory,
            family.FamilyId,
            canSetPoints: false);

        await LoginChildAsync(client, family);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}/points",
            new
            {
                points = 3
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChildPermissions_ShouldBlockEdit_WhenDisabled()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        await SetChildPermissionsAsync(
            factory,
            family.FamilyId,
            canEditTasks: false);

        await LoginChildAsync(client, family);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}",
            new
            {
                title = "Edição bloqueada",
                description = "teste",
                type = "mandatory",
                period = "morning",
                points = 1
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChildPermissions_ShouldBlockDelete_WhenDisabled()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginAdultAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var taskId = await CreateTaskAndGetIdAsync(client);

        await SetChildPermissionsAsync(
            factory,
            family.FamilyId,
            canDeleteTasks: false);

        await LoginChildAsync(client, family);

        var response = await client.DeleteAsync(
            $"/api/v1/daily-tasks/{taskId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task ChildPermissions_Defaults_ShouldAllowChildTaskOperations()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        await LoginChildAsync(client, family);
        await EnsureTodayRoutineAsync(client);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = "Tarefa infantil permitida",
                description = "teste",
                type = "mandatory",
                period = "morning",
                points = 1
            });

        Assert.Equal(
            HttpStatusCode.OK,
            createResponse.StatusCode);

        var routine =
            await createResponse.Content.ReadFromJsonAsync<JsonElement>();

        var taskId =
            routine.GetProperty("tasks")
                .EnumerateArray()
                .Last(t =>
                    t.GetProperty("title").GetString() ==
                    "Tarefa infantil permitida")
                .GetProperty("id")
                .GetString()!;

        var pointsResponse = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}/points",
            new
            {
                points = 2
            });

        Assert.Equal(
            HttpStatusCode.OK,
            pointsResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/daily-tasks/{taskId}",
            new
            {
                title = "Tarefa infantil editada",
                description = "editada",
                type = "expected",
                period = "afternoon",
                points = 2
            });

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/daily-tasks/{taskId}");

        Assert.Equal(
            HttpStatusCode.OK,
            deleteResponse.StatusCode);
    }

    private async Task<string> CreateTaskAndGetIdAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/daily-tasks",
            new
            {
                title = $"Task-{Guid.NewGuid():N}",
                description = "Task criada para o teste",
                type = "expected",
                period = "afternoon",
                points = 2
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var routine =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        var task =
            routine.GetProperty("tasks")
                .EnumerateArray()
                .Last(t =>
                    t.GetProperty("title")
                        .GetString()!
                        .StartsWith("Task-"));

        return task.GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> EnsureTodayRoutineAsync(
        HttpClient client)
    {
        var response =
            await client.GetAsync(
                "/api/v1/daily-routines/today");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content
            .ReadFromJsonAsync<JsonElement>();
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

    private async Task SetChildPermissionsAsync(
        PacusApiFactory factory,
        string familyId,
        bool? canCreateTasks = null,
        bool? canSetPoints = null,
        bool? canEditTasks = null,
        bool? canDeleteTasks = null)
    {
        var database =
            new MongoClient(_mongo.ConnectionString)
                .GetDatabase(factory.DatabaseName);

        var settingsCollection =
            database.GetCollection<
                Pacus.Domain.Entities.Settings>(
                "settings");

        Assert.True(
            MongoDB.Bson.ObjectId.TryParse(
                familyId,
                out var familyObjectId));

        var settings =
            await settingsCollection
                .Find(s => s.FamilyId == familyObjectId)
                .FirstOrDefaultAsync();

        settings ??=
            new Pacus.Domain.Entities.Settings
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                FamilyId = familyObjectId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        settings.ChildPermissions ??=
            new Pacus.Domain.Entities.ChildPermissions();

        if (canCreateTasks.HasValue)
            settings.ChildPermissions.CanCreateTasks =
                canCreateTasks.Value;

        if (canSetPoints.HasValue)
            settings.ChildPermissions.CanSetPoints =
                canSetPoints.Value;

        if (canEditTasks.HasValue)
            settings.ChildPermissions.CanEditTasks =
                canEditTasks.Value;

        if (canDeleteTasks.HasValue)
            settings.ChildPermissions.CanDeleteTasks =
                canDeleteTasks.Value;

        settings.UpdatedAt = DateTime.UtcNow;

        await settingsCollection.ReplaceOneAsync(
            s => s.FamilyId == familyObjectId,
            settings,
            new ReplaceOptions
            {
                IsUpsert = true
            });
    }

    private static JsonElement FindTask(
        JsonElement routine,
        string taskId)
    {
        foreach (var task in
                 routine.GetProperty("tasks")
                     .EnumerateArray())
        {
            if (task.GetProperty("id").GetString() == taskId)
                return task;
        }

        throw new Xunit.Sdk.XunitException(
            $"Tarefa {taskId} não encontrada.");
    }

    private static async Task<TestFamily> BootstrapAsync(
        HttpClient client)
    {
        var suffix =
            Guid.NewGuid()
                .ToString("N")[..8];

        var adultName =
            $"Adulto {suffix}";

        var adultEmail =
            $"adult-{suffix}@test.local";

        var adultPassword =
            "Teste123!";

        var childName =
            $"Crianca {suffix}";

        var childPin =
            "1234";

        var response =
            await client.PostAsJsonAsync(
                "/api/v1/bootstrap",
                new
                {
                    adultName,
                    adultEmail,
                    adultPassword,
                    childName,
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
