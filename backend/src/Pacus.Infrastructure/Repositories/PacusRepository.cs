using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Infrastructure.Mongo;
using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Infrastructure.Repositories;

public class PacusRepository : IPacusRepository
{
    private readonly MongoDbContext _context;

    public PacusRepository(MongoDbContext context) => _context = context;

    public async Task<PacusEntity?> GetByFamilyIdAsync(ObjectId familyId) =>
        await _context.Pacus.Find(p => p.FamilyId == familyId).FirstOrDefaultAsync();

    public async Task<PacusEntity> CreateAsync(PacusEntity pacus)
    {
        await _context.Pacus.InsertOneAsync(pacus);
        return pacus;
    }

    public Task UpdateAsync(PacusEntity pacus) =>
        _context.Pacus.ReplaceOneAsync(p => p.Id == pacus.Id, pacus);

    public async Task<bool> TryApplyGrowthAsync(
        ObjectId familyId,
        string date,
        int totalClosedDays,
        Pacus.Domain.Enums.PacusStage stage,
        double size,
        Pacus.Domain.Entities.PacusStageHistoryEntry? historyEntry,
        DateTime updatedAt)
    {
        var filter = Builders<PacusEntity>.Filter.And(
            Builders<PacusEntity>.Filter.Eq(p => p.FamilyId, familyId),
            Builders<PacusEntity>.Filter.Ne(p => p.LastGrowthDate, date));

        var update = Builders<PacusEntity>.Update
            .Set(p => p.TotalClosedDays, totalClosedDays)
            .Set(p => p.Stage, stage)
            .Set(p => p.Size, size)
            .Set(p => p.LastGrowthDate, date)
            .Set(p => p.UpdatedAt, updatedAt);

        if (historyEntry is not null)
            update = update.Push(p => p.StageHistory, historyEntry);

        var result = await _context.Pacus.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    public Task DeleteByFamilyIdAsync(ObjectId familyId) =>
        _context.Pacus.DeleteManyAsync(p => p.FamilyId == familyId);
}
