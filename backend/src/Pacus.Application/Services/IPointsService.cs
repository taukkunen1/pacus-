using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public interface IPointsService
{
    Task<int> GetBalanceAsync(ObjectId userId);

    // date e a data operacional (YYYY-MM-DD) da transacao. dailyRoutineId e nulo para
    // transacoes que nao pertencem a um dia especifico (ex. Redemption — gasto na loja).
    Task RecordAsync(ObjectId userId, ObjectId? dailyRoutineId, string date, string taskId, string taskTitle,
        PointTransactionType type, int points, ObjectId actorId, UserRole actorRole, string? reason = null);
}
