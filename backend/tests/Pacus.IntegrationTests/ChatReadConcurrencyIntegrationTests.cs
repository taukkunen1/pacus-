using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Pacus.IntegrationTests;

public class ChatReadConcurrencyIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public ChatReadConcurrencyIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task ConcurrentFirstReadWrites_ShouldKeepSingleState()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var adultClient = factory.CreateClient();
        using var childClient = factory.CreateClient();

        var family = await BootstrapAsync(adultClient);
        await LoginAdultAsync(adultClient, family);
        await LoginChildAsync(childClient, family);

        var sent = await adultClient.PostAsJsonAsync(
            "/api/v1/chat/messages",
            new { text = "Cursor concorrente" });
        var sentBody = await sent.Content.ReadFromJsonAsync<JsonElement>();
        var messageId = sentBody.GetProperty("id").GetString();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ =>
                childClient.PutAsJsonAsync(
                    "/api/v1/chat/read",
                    new { lastMessageId = messageId })));

        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var database = new MongoClient(_mongo.ConnectionString)
            .GetDatabase(factory.DatabaseName);
        var states = database.GetCollection<BsonDocument>("chat_read_states");
        var userId = ObjectId.Parse(family.ChildUserId);

        var docs = await states
            .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
            .ToListAsync();

        Assert.Single(docs);
        Assert.Equal(userId, docs[0]["_id"].AsObjectId);
    }

    private static async Task LoginAdultAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new { email = family.AdultEmail, password = family.AdultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                body.GetProperty("token").GetString());
    }

    private static async Task LoginChildAsync(HttpClient client, TestFamily family)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new { userId = family.ChildUserId, pin = family.ChildPin });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                body.GetProperty("token").GetString());
    }

    private static async Task<TestFamily> BootstrapAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adultEmail = $"adult-chat-concurrency-{suffix}@test.local";
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
