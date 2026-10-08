using MongoDB.Bson;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Interfaces;

// A2: confirma o snapshot diario, a transacao de pontos (quando houver) e o
// evento de auditoria numa unica transacao MongoDB. Nunca faz fallback
// silencioso para gravacoes parciais em producao.
public sealed record TaskLedgerDelta(PointTransactionType Type, int Points, string? Reason = null);

public interface ITaskLedgerCommitter
{
    Task CommitAsync(
        DailyRoutine routine,
        TaskEvent auditEvent,
        TaskLedgerDelta? delta = null,
        IReadOnlyList<DailyRoutine>? plannedUpdates = null,
        ObjectId? softDeleteTemplateId = null);
}
