namespace Pacus.IntegrationTests;

public class MongoIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _fixture;

    public MongoIntegrationTests(MongoIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void MongoContainer_ShouldBeAvailable()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));
    }
}