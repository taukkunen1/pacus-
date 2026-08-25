using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pacus.IntegrationTests;

public class AuthHttpIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _mongo;

    public AuthHttpIntegrationTests(MongoIntegrationFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task AdultLogin_ShouldReturnTokenAndCorrectIdentity()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new
            {
                email = family.AdultEmail,
                password = family.AdultPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("token", out var tokenElement));
        Assert.True(body.TryGetProperty("userId", out var userIdElement));
        Assert.True(body.TryGetProperty("role", out var roleElement));
        Assert.True(body.TryGetProperty("name", out var nameElement));
        Assert.True(body.TryGetProperty("expiresAt", out _));

        var token = tokenElement.GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(family.AdultUserId, userIdElement.GetString());
        Assert.Equal("Adult", roleElement.GetString());
        Assert.Equal(family.AdultName, nameElement.GetString());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(family.AdultUserId, jwt.Subject);
        Assert.Equal("Adult", jwt.Claims.First(c =>
            c.Type == "role" ||
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Value);

        var familyClaim = jwt.Claims.First(c => c.Type == "familyId");

        Assert.Equal(family.FamilyId, familyClaim.Value);
    }

    [Fact]
    public async Task AdultLogin_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new
            {
                email = family.AdultEmail,
                password = "SenhaErrada123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Email ou senha invalidos.",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AdultLogin_WithUnknownEmail_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        await BootstrapAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/adult/login",
            new
            {
                email = $"unknown-{Guid.NewGuid():N}@test.local",
                password = "Teste123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChildLogin_ShouldReturnTokenWithChildRoleAndFamily()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

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
        Assert.Equal(family.ChildUserId, body.GetProperty("userId").GetString());
        Assert.Equal("Child", body.GetProperty("role").GetString());
        Assert.Equal(family.ChildName, body.GetProperty("name").GetString());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(family.ChildUserId, jwt.Subject);

        var roleClaim = jwt.Claims.First(c =>
            c.Type == "role" ||
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

        Assert.Equal("Child", roleClaim.Value);

        var familyClaim = jwt.Claims.First(c => c.Type == "familyId");

        Assert.Equal(family.FamilyId, familyClaim.Value);
    }

    [Fact]
    public async Task ChildLogin_WithInvalidPin_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new
            {
                userId = family.ChildUserId,
                pin = "9999"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Perfil ou PIN invalidos.",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ChildLogin_WithInvalidUserId_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        await BootstrapAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new
            {
                userId = "nao-e-object-id",
                pin = "1234"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/pacus/me/habitat");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChildToken_ShouldBeRejectedByAdultOnlyEndpoint()
    {
        using var factory = new PacusApiFactory(_mongo.ConnectionString);
        using var client = factory.CreateClient();

        var family = await BootstrapAsync(client);

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/child/login",
            new
            {
                userId = family.ChildUserId,
                pin = family.ChildPin
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var loginBody =
            await login.Content.ReadFromJsonAsync<JsonElement>();

        var token = loginBody.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync(
            "/api/v1/pacus/me/habitat",
            new
            {
                elements = new
                {
                    water = true,
                    plants = new[] { "plant" },
                    rocks = Array.Empty<string>(),
                    hidingSpots = Array.Empty<string>(),
                    bubbles = true
                },
                bounds = new
                {
                    width = 100,
                    height = 80
                },
                theme = "default"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created
            });

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        return new TestFamily(
            AdultUserId: body.GetProperty("adultUserId").GetString()!,
            ChildUserId: body.GetProperty("childUserId").GetString()!,
            FamilyId: body.GetProperty("familyId").GetString()!,
            AdultName: adultName,
            AdultEmail: adultEmail,
            AdultPassword: adultPassword,
            ChildName: childName,
            ChildPin: childPin);
    }

    private sealed record TestFamily(
        string AdultUserId,
        string ChildUserId,
        string FamilyId,
        string AdultName,
        string AdultEmail,
        string AdultPassword,
        string ChildName,
        string ChildPin);
}
