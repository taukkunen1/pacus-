using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly MongoDbContext _context;

    public SettingsRepository(MongoDbContext context) => _context = context;

    public Task<Settings?> GetByUserIdAsync(ObjectId userId) =>
        _context.Settings.Find(s => s.FamilyId == userId).FirstOrDefaultAsync();

    public Task UpsertAsync(Settings settings) =>
        _context.Settings.ReplaceOneAsync(
            s => s.FamilyId == settings.FamilyId,
            settings,
            new ReplaceOptions { IsUpsert = true });

    public Task DeleteByFamilyIdAsync(ObjectId familyId) =>
        _context.Settings.DeleteManyAsync(s => s.FamilyId == familyId);
}
