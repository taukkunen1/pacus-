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
}
