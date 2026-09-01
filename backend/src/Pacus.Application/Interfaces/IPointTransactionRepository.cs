using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IPointTransactionRepository
{
    Task<PointTransaction> CreateAsync(PointTransaction transaction);
    Task<int> GetBalanceAsync(ObjectId userId);

    // Paginado (achado #4 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md).
    Task<(List<PointTransaction> Items, long TotalCount)> GetHistoryAsync(ObjectId userId, int page, int pageSize);

    // Todas as transacoes, sem limite, para exportacao de dados (B2).
    Task<List<PointTransaction>> GetAllByFamilyAsync(ObjectId familyId);

    // Remove todas as transacoes da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
