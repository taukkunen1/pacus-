using MongoDB.Bson;
using MongoDB.Driver;
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

    public Task<List<AuditLog>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.AuditLogs.Find(a => a.FamilyId == familyId)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync();

    public Task AnonymizeByFamilyAsync(ObjectId familyId, DateTime purgeAt) =>
        _context.AuditLogs.UpdateManyAsync(
            a => a.FamilyId == familyId,
            Builders<AuditLog>.Update
                .Set(a => a.ActorId, ObjectId.Empty)
                .Set(a => a.Anonymized, true)
                .Set(a => a.PurgeAt, purgeAt));
}
