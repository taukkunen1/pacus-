using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.DTOs;

// Define o saldo de Pacus Points para um valor absoluto (em vez de um delta),
// pensado para migrar um saldo que a familia ja tinha antes deste app existir.
// Por baixo, isso gera uma unica transacao do tipo Adjustment com o delta
// necessario para chegar nesse valor — o extrato continua auditavel.
public record SetPointsBalanceRequest(int Balance, string? Reason);

// DTO de resposta pra PointTransaction (mesma ideia do achado #3 da auditoria de API
// de 2026-09-01 -- ver docs/ESTADO_ATUAL.md e DailyRoutineDto.cs): o extrato de pontos
// devolvia a entidade de dominio crua. Aproveitado o mesmo passo que trouxe paginacao
// pro endpoint (achado #4) pra tambem corrigir isso aqui.
public record PointTransactionResponse(
    string Id,
    string FamilyId,
    string Date,
    string? DailyRoutineId,
    string TaskId,
    string TaskTitle,
    PointTransactionType Type,
    int Points,
    int BalanceAfter,
    string? Reason,
    string ActorId,
    UserRole ActorRole,
    DateTime CreatedAt
);

public static class PointTransactionMappingExtensions
{
    public static PointTransactionResponse ToResponse(this PointTransaction transaction) => new(
        transaction.Id.ToString(),
        transaction.FamilyId.ToString(),
        transaction.Date,
        transaction.DailyRoutineId?.ToString(),
        transaction.TaskId,
        transaction.TaskTitle,
        transaction.Type,
        transaction.Points,
        transaction.BalanceAfter,
        transaction.Reason,
        transaction.ActorId.ToString(),
        transaction.ActorRole,
        transaction.CreatedAt);
}
