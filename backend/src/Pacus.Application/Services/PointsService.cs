using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class PointsService : IPointsService
{
    private readonly IPointTransactionRepository _pointTransactionRepository;

    public PointsService(IPointTransactionRepository pointTransactionRepository) =>
        _pointTransactionRepository = pointTransactionRepository;

    public Task<int> GetBalanceAsync(ObjectId userId) =>
        _pointTransactionRepository.GetBalanceAsync(userId);

    public async Task RecordAsync(
        ObjectId userId,
        ObjectId? dailyRoutineId,
        string date,
        string taskId,
        string taskTitle,
        PointTransactionType type,
        int points,
        ObjectId actorId,
        UserRole actorRole,
        string? reason = null)
    {
        // points chega como delta assinado do chamador:
        // Award positivo, Reversal negativo (espelha exatamente o award revertido),
        // Redemption negativo, Adjustment +/-.
        var currentBalance = await _pointTransactionRepository.GetBalanceAsync(userId);

        var transaction = new PointTransaction
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            Date = date,
            DailyRoutineId = dailyRoutineId,
            TaskId = taskId,
            TaskTitle = taskTitle,
            Type = type,
            Points = points,
            BalanceAfter = currentBalance + points,
            Reason = reason,
            ActorId = actorId,
            ActorRole = actorRole,
            CreatedAt = DateTime.UtcNow,
        };

        await _pointTransactionRepository.CreateAsync(transaction);
    }
}
