using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController(MongoDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return Ok(new { status = "ok", database = "connected", timestamp = DateTime.UtcNow });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = "degraded", database = "unavailable", timestamp = DateTime.UtcNow });
        }
    }
}
