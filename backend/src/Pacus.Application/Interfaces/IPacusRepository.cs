using MongoDB.Bson;
using Pacus.Domain.Entities;

using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Application.Interfaces;

public interface IPacusRepository
{
    Task<PacusEntity?> GetByFamilyIdAsync(ObjectId familyId);
    Task<PacusEntity> CreateAsync(PacusEntity pacus);
    Task UpdateAsync(PacusEntity pacus);

    // Remove o pacus da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteByFamilyIdAsync(ObjectId familyId);
}
