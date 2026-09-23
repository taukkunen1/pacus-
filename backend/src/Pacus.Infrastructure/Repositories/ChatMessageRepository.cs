using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly MongoDbContext _context;

    public ChatMessageRepository(MongoDbContext context) => _context = context;

    public async Task<ChatMessage> CreateAsync(ChatMessage message)
    {
        await _context.ChatMessages.InsertOneAsync(message);
        return message;
    }

    public async Task<List<ChatMessage>> GetRecentByFamilyAsync(
        ObjectId familyId,
        ObjectId? afterId = null,
        int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 100);

        var familyFilter = Builders<ChatMessage>.Filter.Eq(m => m.FamilyId, familyId);

        if (afterId.HasValue)
        {
            var filter = familyFilter &
                         Builders<ChatMessage>.Filter.Gt(m => m.Id, afterId.Value);

            return await _context.ChatMessages
                .Find(filter)
                .SortBy(m => m.Id)
                .Limit(limit)
                .ToListAsync();
        }

        var latest = await _context.ChatMessages
            .Find(familyFilter)
            .SortByDescending(m => m.Id)
            .Limit(limit)
            .ToListAsync();

        latest.Reverse();
        return latest;
    }

    public Task<List<ChatMessage>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatMessages
            .Find(m => m.FamilyId == familyId)
            .SortBy(m => m.Id)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatMessages.DeleteManyAsync(m => m.FamilyId == familyId);
}
