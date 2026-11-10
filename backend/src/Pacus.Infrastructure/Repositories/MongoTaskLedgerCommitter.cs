using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Exceptions;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

// A2: uma unica transacao para o estado da tarefa, ledger e auditoria.
// MongoDB Atlas e os testes usam replica set (transacoes nao funcionam em standalone).
// Em erro de escrita/commit nao grava parcialmente e nao faz fallback nao transacional.
public sealed class MongoTaskLedgerCommitter : ITaskLedgerCommitter
{
    private readonly MongoDbContext _context;
    public MongoTaskLedgerCommitter(MongoDbContext context) => _context = context;

    public async Task CommitAsync(
        DailyRoutine routine,
        TaskEvent auditEvent,
        TaskLedgerDelta? delta = null,
        IReadOnlyList<DailyRoutine>? plannedUpdates = null,
        ObjectId? softDeleteTemplateId = null)
    {
        if (auditEvent.UserId != routine.FamilyId || auditEvent.DailyRoutineId != routine.Id)
            throw new ValidationException("Auditoria da tarefa nao pertence a rotina.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == auditEvent.TaskId);
        if (task is null)
            throw new ValidationException("Tarefa da auditoria nao foi encontrada na rotina.");
        if (delta is not null && delta.Points == 0)
            throw new ValidationException("Lancamento financeiro sem variacao de pontos.");

        var extra = plannedUpdates ?? Array.Empty<DailyRoutine>();
        if (extra.Any(r => r.FamilyId != routine.FamilyId || r.Status != RoutineStatus.Planned))
            throw new ValidationException("Rotina planejada invalida para a transacao.");
        if (extra.Any(r => r.Id == routine.Id))
            throw new ValidationException("A rotina principal nao pode aparecer duas vezes.");

        var expectedVersions = new Dictionary<DailyRoutine, int> { [routine] = routine.Version };
        foreach (var planned in extra)
            expectedVersions.Add(planned, planned.Version);

        using var session = await _context.Database.Client.StartSessionAsync();
        session.StartTransaction(new TransactionOptions(
            readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority));

        try
        {
            await ReplaceVersionedAsync(session, routine);

            foreach (var planned in extra)
                await ReplaceVersionedAsync(session, planned);

            if (softDeleteTemplateId is ObjectId templateId)
            {
                var filter = Builders<TaskTemplate>.Filter.Eq(t => t.Id, templateId) &
                             Builders<TaskTemplate>.Filter.Eq(t => t.FamilyId, routine.FamilyId) &
                             Builders<TaskTemplate>.Filter.Eq(t => t.Active, true);
                var update = Builders<TaskTemplate>.Update
                    .Set(t => t.Active, false)
                    .Set(t => t.DeletedAt, DateTime.UtcNow);
                var result = await _context.TaskTemplates.UpdateOneAsync(session, filter, update);
                if (result.MatchedCount != 1)
                    throw new ConflictException("A tarefa permanente foi alterada durante a exclusao.");
            }

            if (delta is not null)
            {
                // Saldo oficial e a soma do ledger. Snapshot balanceAfter usa a mesma
                // visao transacional; escritas de outras origens podem criar snapshots
                // de exibicao defasados, mas nao alteram a soma oficial.
                var currentBalance = await _context.PointTransactions
                    .Aggregate(session)
                    .Match(t => t.FamilyId == routine.FamilyId)
                    .Group(t => 1, g => new { Total = g.Sum(t => t.Points) })
                    .FirstOrDefaultAsync();

                var entry = new PointTransaction
                {
                    Id = ObjectId.GenerateNewId(),
                    FamilyId = routine.FamilyId,
                    Date = routine.Date,
                    DailyRoutineId = routine.Id,
                    TaskId = task.Id,
                    TaskTitle = task.Title,
                    Type = delta.Type,
                    SourceType = delta.Type switch
                    {
                        PointTransactionType.Redemption => "redemption",
                        PointTransactionType.Adjustment => "adjustment",
                        _ => "task",
                    },
                    SourceId = task.Id,
                    Points = delta.Points,
                    BalanceAfter = (currentBalance?.Total ?? 0) + delta.Points,
                    Reason = delta.Reason,
                    ActorId = auditEvent.ActorId,
                    ActorRole = auditEvent.ActorRole,
                    CreatedAt = auditEvent.CreatedAt,
                };
                await _context.PointTransactions.InsertOneAsync(session, entry);
            }

            await _context.TaskEvents.InsertOneAsync(session, auditEvent);

            // Commit incerto: repetir somente o COMMIT da mesma transacao (nunca
            // repetir insert/Replace nem conceder pontos de novo).
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await session.CommitTransactionAsync();
                    break;
                }
                catch (MongoException ex) when (
                    ex.HasErrorLabel("UnknownTransactionCommitResult") && attempt < 3)
                {
                    // O servidor pode ja ter confirmado; o driver permite repetir commit.
                }
            }
        }
        catch (Exception ex)
        {
            if (session.IsInTransaction)
            {
                try { await session.AbortTransactionAsync(); }
                catch (MongoException) { /* preserva a excecao original */ }
            }

            foreach (var (r, version) in expectedVersions)
                r.Version = version;

            if (ex is MongoException mongo && mongo.HasErrorLabel("TransientTransactionError"))
                throw new ConflictException("A rotina foi alterada simultaneamente. Atualize e tente novamente.");
            throw;
        }
    }

    private async Task ReplaceVersionedAsync(
        IClientSessionHandle session, DailyRoutine routine)
    {
        var expectedVersion = routine.Version;
        var f = Builders<DailyRoutine>.Filter;
        var filter = f.Eq(r => r.Id, routine.Id) &
                     f.Eq(r => r.FamilyId, routine.FamilyId) &
                     (expectedVersion == 0
                        ? f.Or(f.Eq(r => r.Version, 0), f.Exists(r => r.Version, false))
                        : f.Eq(r => r.Version, expectedVersion));

        routine.Version = expectedVersion + 1;
        var result = await _context.DailyRoutines.ReplaceOneAsync(session, filter, routine);
        if (result.MatchedCount != 1)
            throw new ConflictException("Rotina atualizada em outra requisicao. Atualize e tente novamente.");
    }
}
