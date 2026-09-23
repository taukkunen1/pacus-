using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class ChatReadStateRepository : IChatReadStateRepository
{
    private readonly MongoDbContext _context;

    public ChatReadStateRepository(MongoDbContext context) => _context = context;

    public Task<ChatReadState?> GetAsync(ObjectId familyId, ObjectId userId) =>
        _context.ChatReadStates
            .Find(s => s.FamilyId == familyId && s.UserId == userId)
            .FirstOrDefaultAsync();

    public async Task UpsertAsync(
        ObjectId familyId,
        ObjectId userId,
        DateTime lastReadAt)
    {
        var filter =
            Builders<ChatReadState>.Filter.Eq(s => s.FamilyId, familyId) &
            Builders<ChatReadState>.Filter.Eq(s => s.UserId, userId);

        var update = Builders<ChatReadState>.Update
            .SetOnInsert(s => s.Id, ObjectId.GenerateNewId())
            .SetOnInsert(s => s.FamilyId, familyId)
            .SetOnInsert(s => s.UserId, userId)
            .Max(s => s.LastReadAt, lastReadAt)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        await _context.ChatReadStates.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true });
    }

    public Task<List<ChatReadState>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatReadStates
            .Find(s => s.FamilyId == familyId)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatReadStates.DeleteManyAsync(s => s.FamilyId == familyId);
}
