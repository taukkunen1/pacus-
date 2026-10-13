using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IWaterIntakeRepository
{
    Task<WaterIntake?> GetByEventIdAsync(ObjectId familyId, ObjectId actorId, string eventId);
    Task CreateAsync(WaterIntake intake);
    Task<List<WaterIntake>> GetByDateAsync(ObjectId familyId, string date, ObjectId actorId);
    Task<WaterIntake?> GetLatestByDateAsync(ObjectId familyId, string date, ObjectId actorId);
    Task<bool> DeleteAsync(ObjectId familyId, ObjectId id, ObjectId actorId);
    Task DeleteByFamilyIdAsync(ObjectId familyId);
}
