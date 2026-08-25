using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IPointTransactionRepository
{
    Task<PointTransaction> CreateAsync(PointTransaction transaction);
    Task<int> GetBalanceAsync(ObjectId userId);
    Task<List<PointTransaction>> GetHistoryAsync(ObjectId userId, int limit = 100);
}
