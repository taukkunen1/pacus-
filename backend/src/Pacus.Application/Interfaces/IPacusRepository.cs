using MongoDB.Bson;
using Pacus.Domain.Entities;

using PacusEntity = Pacus.Domain.Entities.Pacus;

namespace Pacus.Application.Interfaces;

public interface IPacusRepository
{
    Task<PacusEntity?> GetByFamilyIdAsync(ObjectId familyId);
    Task<PacusEntity> CreateAsync(PacusEntity pacus);
    Task UpdateAsync(PacusEntity pacus);
    // Crescimento idempotente atomico: so atualiza se esta data ainda nao foi processada.
    Task<bool> TryApplyGrowthAsync(
        ObjectId familyId, string date, int totalClosedDays, Pacus.Domain.Enums.PacusStage stage,
        double size, PacusStageHistoryEntry? historyEntry, DateTime updatedAt);

    // Remove o pacus da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteByFamilyIdAsync(ObjectId familyId);
}
