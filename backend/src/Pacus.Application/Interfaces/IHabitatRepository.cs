using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IHabitatRepository
{
    Task<Habitat?> GetByFamilyIdAsync(ObjectId familyId);
    Task<Habitat> UpsertAsync(Habitat habitat);
}
