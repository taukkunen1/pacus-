using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MongoDB.Driver;

namespace Pacus.IntegrationTests;

public sealed class MongoIntegrationFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public MongoIntegrationFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("mongo:8")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilPortIsAvailable(27017))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(27017);

        ConnectionString = $"mongodb://{host}:{port}";

        var client = new MongoClient(ConnectionString);

        // Garante que o Mongo respondeu antes de iniciar os testes.
        await client
            .GetDatabase("admin")
            .RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}