using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IWaterIntakeRepository
{
    Task<WaterIntake?> GetByEventIdAsync(ObjectId familyId, string eventId);
    Task CreateAsync(WaterIntake intake);
    Task<List<WaterIntake>> GetByDateAsync(ObjectId familyId, string date);
    Task<WaterIntake?> GetLatestByDateAsync(ObjectId familyId, string date);
    Task<bool> DeleteAsync(ObjectId familyId, ObjectId id);
    Task DeleteByFamilyIdAsync(ObjectId familyId);
}
