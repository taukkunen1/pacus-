using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IChatMessageRepository
{
    Task<ChatMessage> CreateAsync(ChatMessage message);
    Task<List<ChatMessage>> GetRecentByFamilyAsync(
        ObjectId familyId,
        ObjectId? afterId = null,
        int limit = 100);
    Task<ChatMessage?> GetByIdForFamilyAsync(ObjectId familyId, ObjectId messageId);
    Task<long> CountUnreadAsync(
        ObjectId familyId,
        ObjectId userId,
        ObjectId? afterId);
    Task<List<ChatMessage>> GetAllByFamilyAsync(ObjectId familyId);
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
