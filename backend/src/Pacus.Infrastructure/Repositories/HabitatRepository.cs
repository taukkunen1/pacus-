using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class HabitatRepository : IHabitatRepository
{
    private readonly MongoDbContext _context;

    public HabitatRepository(MongoDbContext context) => _context = context;

    public async Task<Habitat?> GetByFamilyIdAsync(ObjectId familyId)
    {
        return await _context.Habitats
            .Find(h => h.FamilyId == familyId)
            .FirstOrDefaultAsync();
    }

    public async Task<Habitat> UpsertAsync(Habitat habitat)
    {
        await _context.Habitats.ReplaceOneAsync(
            h => h.FamilyId == habitat.FamilyId,
            habitat,
            new ReplaceOptions { IsUpsert = true });

        return habitat;
    }

    public Task DeleteByFamilyIdAsync(ObjectId familyId) =>
        _context.Habitats.DeleteManyAsync(h => h.FamilyId == familyId);
}
