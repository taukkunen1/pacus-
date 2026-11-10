using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Pacus.IntegrationTests;

// Uma instancia MongoDB isolada por fixture, com replica set para transacoes.
// Cada PacusApiFactory ainda usa um database exclusivo (pacus_api_test_*).
public sealed class MongoIntegrationFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public MongoIntegrationFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("mongo:8")
            .WithCommand("mongod", "--replSet", "rs0", "--bind_ip_all")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilPortIsAvailable(27017))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var init = await _container.ExecAsync(new[]
        {
            "mongosh", "--quiet", "--eval",
            "rs.initiate({_id:'rs0',members:[{_id:0,host:'localhost:27017'}]})"
        });
        if (init.ExitCode != 0)
            throw new InvalidOperationException("Falha ao iniciar replica set Mongo de testes: " + init.Stderr);

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(27017);
        ConnectionString = $"mongodb://{host}:{port}/?directConnection=true";
        var client = new MongoClient(ConnectionString);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var hello = await client.GetDatabase("admin")
                    .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (hello.GetValue("isWritablePrimary", false).ToBoolean())
                    return;
            }
            catch (MongoException)
            {
                // A eleicao de PRIMARY ainda pode estar acontecendo.
            }
            await Task.Delay(500);
        }
        throw new TimeoutException("Replica set de testes nao ficou PRIMARY.");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
