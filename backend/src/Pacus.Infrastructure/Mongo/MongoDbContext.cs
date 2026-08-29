using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Pacus.Domain.Entities;
using System.Security.Authentication;

using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Infrastructure.Mongo;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    static MongoDbContext()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention()
        };

        ConventionRegistry.Register(
            "camelCase",
            pack,
            _ => true
        );
    }

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var mongoUrl = new MongoUrl(
            settings.Value.ConnectionString
        );

        var mongoSettings = MongoClientSettings.FromUrl(
            mongoUrl
        );

        mongoSettings.SslSettings = new SslSettings
        {
            EnabledSslProtocols = SslProtocols.Tls12
        };

        var client = new MongoClient(
            mongoSettings
        );

        _database = client.GetDatabase(
            settings.Value.DatabaseName
        );
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<User> Users =>
        _database.GetCollection<User>("users");

    public IMongoCollection<PacusEntity> Pacus =>
        _database.GetCollection<PacusEntity>("pacus");

    public IMongoCollection<DailyRoutine> DailyRoutines =>
        _database.GetCollection<DailyRoutine>("daily_routines");

    public IMongoCollection<TaskTemplate> TaskTemplates =>
        _database.GetCollection<TaskTemplate>("task_templates");

    public IMongoCollection<PointTransaction> PointTransactions =>
        _database.GetCollection<PointTransaction>("point_transactions");

    public IMongoCollection<PacusGrowthLog> PacusGrowthLogs =>
        _database.GetCollection<PacusGrowthLog>("pacus_growth");

    public IMongoCollection<TaskEvent> TaskEvents =>
        _database.GetCollection<TaskEvent>("task_events");

    public IMongoCollection<Habitat> Habitats =>
        _database.GetCollection<Habitat>("habitats");

    public IMongoCollection<Settings> Settings =>
        _database.GetCollection<Settings>("settings");

    public IMongoCollection<StoreItem> StoreItems =>
        _database.GetCollection<StoreItem>("store_items");

    public IMongoCollection<Redemption> Redemptions =>
        _database.GetCollection<Redemption>("redemptions");

    public IMongoCollection<AuditLog> AuditLogs =>
        _database.GetCollection<AuditLog>("audit_logs");
}