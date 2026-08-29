using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IPointTransactionRepository
{
    Task<PointTransaction> CreateAsync(PointTransaction transaction);
    Task<int> GetBalanceAsync(ObjectId userId);
    Task<List<PointTransaction>> GetHistoryAsync(ObjectId userId, int limit = 100);

    // Todas as transacoes, sem limite, para exportacao de dados (B2).
    Task<List<PointTransaction>> GetAllByFamilyAsync(ObjectId familyId);
}
