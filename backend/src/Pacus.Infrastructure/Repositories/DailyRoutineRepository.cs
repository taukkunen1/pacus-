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

    // Paginado (achado #4 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md):
    // antes devolvia a lista inteira de dias encerrados sem limite, que so cresce com o
    // tempo (um documento a mais por dia). TotalCount vem de uma segunda query
    // (CountDocumentsAsync) -- o Mongo nao devolve isso de graca junto com Skip/Limit.
    public async Task<(List<DailyRoutine> Items, long TotalCount)> GetHistoryAsync(
        ObjectId userId, string? from, string? to, int page, int pageSize)
    {
        var filterBuilder = Builders<DailyRoutine>.Filter;
        var filter = filterBuilder.Eq(r => r.FamilyId, userId) &
                     filterBuilder.Eq(r => r.Status, Domain.Enums.RoutineStatus.Closed);

        if (!string.IsNullOrEmpty(from))
            filter &= filterBuilder.Gte(r => r.Date, from);
        if (!string.IsNullOrEmpty(to))
            filter &= filterBuilder.Lte(r => r.Date, to);

        var totalCount = await _context.DailyRoutines.CountDocumentsAsync(filter);
        var items = await _context.DailyRoutines.Find(filter)
            .SortByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
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
