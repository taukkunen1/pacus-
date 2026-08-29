using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IPacusGrowthRepository
{
    // Chave logica {userId, date} e unica no banco — segunda tentativa de log
    // para o mesmo dia deve ser tratada como no-op pelo chamador (idempotencia).
    Task<PacusGrowthLog?> GetByUserAndDateAsync(ObjectId userId, string date);
    Task<PacusGrowthLog> CreateAsync(PacusGrowthLog log);

    // Todos os logs de crescimento, para exportacao de dados (B2).
    Task<List<PacusGrowthLog>> GetAllByFamilyAsync(ObjectId familyId);

    // Remove todos os logs de crescimento da familia -- exclusao de conta (LGPD, item B3).
    // Nota: o campo se chama UserId mas guarda o FamilyId (nao foi renomeado no A4) -- ver docs/DATA_MAP.md.
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
