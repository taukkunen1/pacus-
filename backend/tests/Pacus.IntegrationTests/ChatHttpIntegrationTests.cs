using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class ChatHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public ChatHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Chat_AllowsAdultAndChildToExchangeMessages()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();

        var family = await BootstrapAsync(adultClient);

        await LoginAdultAsync(adultClient, family);

        var sendAdult = await adultClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Oi Hector" });

        Assert.Equal(HttpStatusCode.OK, sendAdult.StatusCode);

        var adultMessage = await sendAdult.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(family.AdultUserId, adultMessage.GetProperty("senderId").GetString());
        Assert.Equal("Oi Hector", adultMessage.GetProperty("text").GetString());

        await LoginChildAsync(childClient, family);

        var childView = await childClient.GetAsync("/api/v1/chat/messages");
        Assert.Equal(HttpStatusCode.OK, childView.StatusCode);

        var childMessages = (await childView.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .ToList();

        Assert.Single(childMessages);
        Assert.Equal("Oi Hector", childMessages[0].GetProperty("text").GetString());

        var sendChild = await childClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Oi!" });

        Assert.Equal(HttpStatusCode.OK, sendChild.StatusCode);

        var adultView = await adultClient.GetAsync("/api/v1/chat/messages");
        var adultMessages = (await adultView.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, adultMessages.Count);
        Assert.Equal("Oi!", adultMessages[1].GetProperty("text").GetString());
        Assert.Equal(family.ChildUserId, adultMessages[1].GetProperty("senderId").GetString());
    }

    [Fact]
    public async Task Chat_IsIsolatedByFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstFamily = await BootstrapAsync(firstClient);
        var secondFamily = await BootstrapAsync(secondClient);

        await LoginAdultAsync(firstClient, firstFamily);
        await LoginAdultAsync(secondClient, secondFamily);

        var sent = await firstClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Mensagem privada da familia A" });

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var secondFamilyView = await secondClient.GetAsync("/api/v1/chat/messages");
        Assert.Equal(HttpStatusCode.OK, secondFamilyView.StatusCode);

        var messages = (await secondFamilyView.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .ToList();

        Assert.Empty(messages);
    }

    [Fact]
    public async Task Chat_RequiresAuthentication()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/chat/messages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_RejectsBlankAndOversizedMessages()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var blank = await client.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var oversized = await client.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = new string('x', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [Fact]
    public async Task Chat_UnreadCount_ShouldTrackReadStatePerUser()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();

        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var sent = await adultClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Mensagem nao lida" });

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var sentBody = await sent.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = sentBody.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(messageId));

        var childUnread = await childClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/chat/unread-count");
        Assert.Equal(1, childUnread.GetProperty("unreadCount").GetInt64());

        var adultUnread = await adultClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/chat/unread-count");
        Assert.Equal(0, adultUnread.GetProperty("unreadCount").GetInt64());

        var markRead = await childClient.PutAsJsonAsync(
            "/api/v1/chat/read",
            new { lastMessageId = messageId });

        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);
        var readBody = await markRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, readBody.GetProperty("unreadCount").GetInt64());

        await adultClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Nova mensagem" });

        childUnread = await childClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/chat/unread-count");
        Assert.Equal(1, childUnread.GetProperty("unreadCount").GetInt64());
    }

    [Fact]
    public async Task Chat_MarkRead_ShouldNotAcceptMessageFromAnotherFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstFamily = await BootstrapAsync(firstClient);
        var secondFamily = await BootstrapAsync(secondClient);

        await LoginAdultAsync(firstClient, firstFamily);
        await LoginAdultAsync(secondClient, secondFamily);

        var sent = await firstClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Privada A" });
        var sentBody = await sent.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = sentBody.GetProperty("id").GetString();

        var response = await secondClient.PutAsJsonAsync(
            "/api/v1/chat/read",
            new { lastMessageId = messageId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Chat_ChildCanCreateQuickRequest_AndAdultSeesPending()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();

        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var created = await childClient.PostAsJsonAsync(
            "/api/v1/chat/requests",
            new { type = "help" });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("request", body.GetProperty("kind").GetString());
        Assert.Equal("help", body.GetProperty("requestType").GetString());
        Assert.Equal("pending", body.GetProperty("requestStatus").GetString());

        var adultSummary = await adultClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/chat/unread-count");

        Assert.Equal(0, adultSummary.GetProperty("unreadCount").GetInt64());
        Assert.Equal(1, adultSummary.GetProperty("pendingRequests").GetInt64());
    }

    [Fact]
    public async Task Chat_AdultCannotCreateChildQuickRequest()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);
        await LoginAdultAsync(client, family);

        var response = await client.PostAsJsonAsync(
            "/api/v1/chat/requests",
            new { type = "help" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chat_ApproveExtraTimeRequest_AppliesMinutesOnce()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();

        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var before = await adultClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/daily-routines/today");
        var beforeMinutes =
            before.GetProperty("gameTimerMinutes").GetInt32() +
            before.GetProperty("gameTimerExtraMinutes").GetInt32();

        var created = await childClient.PostAsJsonAsync(
            "/api/v1/chat/requests",
            new { type = "extra_time", minutes = 10 });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var requestBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = requestBody.GetProperty("id").GetString();

        var approve = await adultClient.PutAsJsonAsync(
            $"/api/v1/chat/requests/{requestId}/approve",
            new { });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = await approve.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", approved.GetProperty("requestStatus").GetString());

        var after = await adultClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/daily-routines/today");
        var afterMinutes =
            after.GetProperty("gameTimerMinutes").GetInt32() +
            after.GetProperty("gameTimerExtraMinutes").GetInt32();

        Assert.Equal(beforeMinutes + 10, afterMinutes);

        var childSummary = await childClient.GetFromJsonAsync<JsonElement>(
            "/api/v1/chat/unread-count");
        Assert.Equal(1, childSummary.GetProperty("unreadCount").GetInt64());

        var secondApprove = await adultClient.PutAsJsonAsync(
            $"/api/v1/chat/requests/{requestId}/approve",
            new { });

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    private static async Task LoginAdultAsync(HttpClient client, TestFamily family)
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

    private static async Task LoginChildAsync(HttpClient client, TestFamily family)
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
        client.DefaultRequestHeaders.Authorization = null;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adultEmail = $"adult-chat-{suffix}@test.local";
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
            adultEmail,
            adultPassword,
            childPin);
    }

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string AdultEmail,
        string AdultPassword,
        string ChildPin);
}
