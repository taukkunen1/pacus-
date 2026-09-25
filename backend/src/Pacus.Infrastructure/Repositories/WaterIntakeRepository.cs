using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class WaterIntakeRepository : IWaterIntakeRepository
{
    private readonly MongoDbContext _context;
    public WaterIntakeRepository(MongoDbContext context) => _context = context;

    public Task<WaterIntake?> GetByEventIdAsync(ObjectId familyId, string eventId) =>
        _context.WaterIntakes.Find(x => x.FamilyId == familyId && x.EventId == eventId)
            .FirstOrDefaultAsync();

    public Task CreateAsync(WaterIntake intake) => _context.WaterIntakes.InsertOneAsync(intake);

    public Task<List<WaterIntake>> GetByDateAsync(ObjectId familyId, string date) =>
        _context.WaterIntakes.Find(x => x.FamilyId == familyId && x.Date == date)
            .SortByDescending(x => x.OccurredAt).ToListAsync();

    public Task<WaterIntake?> GetLatestByDateAsync(ObjectId familyId, string date) =>
        _context.WaterIntakes.Find(x => x.FamilyId == familyId && x.Date == date)
            .SortByDescending(x => x.OccurredAt).FirstOrDefaultAsync();

    public async Task<bool> DeleteAsync(ObjectId familyId, ObjectId id) =>
        (await _context.WaterIntakes.DeleteOneAsync(x => x.FamilyId == familyId && x.Id == id)).DeletedCount == 1;

    public Task DeleteByFamilyIdAsync(ObjectId familyId) =>
        _context.WaterIntakes.DeleteManyAsync(x => x.FamilyId == familyId);
}
