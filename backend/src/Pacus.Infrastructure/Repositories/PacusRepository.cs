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

    public Task<PacusEntity?> GetByFamilyIdAsync(ObjectId familyId) =>
        _context.Pacus.Find(p => p.FamilyId == familyId).FirstOrDefaultAsync();

    public async Task<PacusEntity> CreateAsync(PacusEntity pacus)
    {
        await _context.Pacus.InsertOneAsync(pacus);
        return pacus;
    }

    public Task UpdateAsync(PacusEntity pacus) =>
        _context.Pacus.ReplaceOneAsync(p => p.Id == pacus.Id, pacus);
}
