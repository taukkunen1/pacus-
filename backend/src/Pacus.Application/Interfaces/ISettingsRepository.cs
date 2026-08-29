using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ISettingsRepository
{
    Task<Settings?> GetByUserIdAsync(ObjectId userId);
    Task UpsertAsync(Settings settings);

    // Remove as configuracoes da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteByFamilyIdAsync(ObjectId familyId);
}
