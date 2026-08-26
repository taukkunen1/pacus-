using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pacus.IntegrationTests;

public sealed class PacusApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public string DatabaseName => _databaseName;

    public PacusApiFactory(string connectionString)
    {
        _connectionString = connectionString;
        _databaseName = $"pacus_api_test_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MongoDb:ConnectionString"] = _connectionString,
                    ["MongoDb:DatabaseName"] = _databaseName,
                    ["Jwt:Issuer"] = "pacus-api",
                    ["Jwt:Audience"] = "pacus-clients",
                    ["Jwt:Secret"] =
                        "integration-test-secret-012345678901234567890123456789",
                    ["Cors:AllowedOrigins"] = "http://localhost:5500"
                });
        });
    }
}
