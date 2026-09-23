using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IChatReadStateRepository
{
    Task<ChatReadState?> GetAsync(ObjectId familyId, ObjectId userId);
    Task UpsertAsync(ObjectId familyId, ObjectId userId, ObjectId lastReadMessageId, DateTime lastReadAt);
    Task<List<ChatReadState>> GetAllByFamilyAsync(ObjectId familyId);
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
