namespace Pacus.Infrastructure.Mongo;

// Populado via variaveis de ambiente / appsettings — nunca hardcoded, nunca commitado.
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "pacus";
}
