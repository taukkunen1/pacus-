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

    public Task<ChatMessage?> GetByIdForFamilyAsync(
        ObjectId familyId,
        ObjectId messageId) =>
        _context.ChatMessages
            .Find(m => m.FamilyId == familyId && m.Id == messageId)
            .FirstOrDefaultAsync();

    public Task<long> CountUnreadAsync(
        ObjectId familyId,
        ObjectId userId,
        ObjectId? afterId)
    {
        var filter =
            Builders<ChatMessage>.Filter.Eq(m => m.FamilyId, familyId) &
            Builders<ChatMessage>.Filter.Ne(m => m.SenderId, userId) &
            Builders<ChatMessage>.Filter.Ne(m => m.Kind, "request");

        if (afterId.HasValue)
        {
            filter &= Builders<ChatMessage>.Filter.Gt(
                m => m.Id,
                afterId.Value);
        }

        return _context.ChatMessages.CountDocumentsAsync(filter);
    }

    public Task<long> CountPendingRequestsAsync(ObjectId familyId) =>
        _context.ChatMessages.CountDocumentsAsync(
            m => m.FamilyId == familyId &&
                 m.Kind == "request" &&
                 m.RequestStatus == "pending");

    public async Task<ChatMessage?> TryTransitionRequestAsync(
        ObjectId familyId,
        ObjectId messageId,
        string expectedStatus,
        string newStatus,
        ObjectId? reviewedBy = null,
        DateTime? reviewedAt = null)
    {
        var filter =
            Builders<ChatMessage>.Filter.Eq(m => m.FamilyId, familyId) &
            Builders<ChatMessage>.Filter.Eq(m => m.Id, messageId) &
            Builders<ChatMessage>.Filter.Eq(m => m.Kind, "request") &
            Builders<ChatMessage>.Filter.Eq(m => m.RequestStatus, expectedStatus);

        var update = Builders<ChatMessage>.Update
            .Set(m => m.RequestStatus, newStatus)
            .Set(m => m.ReviewedBy, reviewedBy)
            .Set(m => m.ReviewedAt, reviewedAt);

        return await _context.ChatMessages.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ChatMessage>
            {
                ReturnDocument = ReturnDocument.After
            });
    }

    public Task<List<ChatMessage>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatMessages
            .Find(m => m.FamilyId == familyId)
            .SortBy(m => m.Id)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatMessages.DeleteManyAsync(m => m.FamilyId == familyId);
}
