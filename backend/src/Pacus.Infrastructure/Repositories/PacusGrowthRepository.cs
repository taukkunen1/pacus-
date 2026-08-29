using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class PacusGrowthRepository : IPacusGrowthRepository
{
    private readonly MongoDbContext _context;

    public PacusGrowthRepository(MongoDbContext context) => _context = context;

    public Task<PacusGrowthLog?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        _context.PacusGrowthLogs.Find(l => l.UserId == userId && l.Date == date).FirstOrDefaultAsync();

    public async Task<PacusGrowthLog> CreateAsync(PacusGrowthLog log)
    {
        try
        {
            await _context.PacusGrowthLogs.InsertOneAsync(log);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Indice unico {userId, date} barrou uma segunda tentativa de crescimento no mesmo dia.
            // Nao e um erro do ponto de vista de negocio — apenas devolve o log ja existente.
            var existing = await GetByUserAndDateAsync(log.UserId, log.Date);
            if (existing is not null) return existing;
            throw;
        }
        return log;
    }

    public Task<List<PacusGrowthLog>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.PacusGrowthLogs.Find(l => l.UserId == familyId)
            .SortByDescending(l => l.Date)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.PacusGrowthLogs.DeleteManyAsync(l => l.UserId == familyId);
}
