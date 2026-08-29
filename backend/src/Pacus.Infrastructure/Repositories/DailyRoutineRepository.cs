using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class DailyRoutineRepository : IDailyRoutineRepository
{
    private readonly MongoDbContext _context;

    public DailyRoutineRepository(MongoDbContext context) => _context = context;

    public Task<DailyRoutine?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        _context.DailyRoutines.Find(r => r.FamilyId == userId && r.Date == date).FirstOrDefaultAsync();

    public async Task<DailyRoutine> CreateAsync(DailyRoutine routine)
    {
        await _context.DailyRoutines.InsertOneAsync(routine);
        return routine;
    }

    public Task UpdateAsync(DailyRoutine routine) =>
        _context.DailyRoutines.ReplaceOneAsync(r => r.Id == routine.Id, routine);

    public async Task<List<DailyRoutine>> GetHistoryAsync(ObjectId userId, string? from, string? to)
    {
        var filterBuilder = Builders<DailyRoutine>.Filter;
        var filter = filterBuilder.Eq(r => r.FamilyId, userId) &
                     filterBuilder.Eq(r => r.Status, Domain.Enums.RoutineStatus.Closed);

        if (!string.IsNullOrEmpty(from))
            filter &= filterBuilder.Gte(r => r.Date, from);
        if (!string.IsNullOrEmpty(to))
            filter &= filterBuilder.Lte(r => r.Date, to);

        return await _context.DailyRoutines.Find(filter)
            .SortByDescending(r => r.Date)
            .ToListAsync();
    }

    public Task<DailyRoutine?> GetLatestOpenAsync(ObjectId userId) =>
        _context.DailyRoutines
            .Find(r => r.FamilyId == userId && r.Status == Domain.Enums.RoutineStatus.Open)
            .SortByDescending(r => r.Date)
            .FirstOrDefaultAsync();

    public Task<List<DailyRoutine>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.DailyRoutines.Find(r => r.FamilyId == familyId)
            .SortByDescending(r => r.Date)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.DailyRoutines.DeleteManyAsync(r => r.FamilyId == familyId);
}
