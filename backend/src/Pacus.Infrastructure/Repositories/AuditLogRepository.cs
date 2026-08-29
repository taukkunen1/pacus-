using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly MongoDbContext _context;

    public AuditLogRepository(MongoDbContext context) => _context = context;

    public async Task<AuditLog> CreateAsync(AuditLog log)
    {
        await _context.AuditLogs.InsertOneAsync(log);
        return log;
    }
}
