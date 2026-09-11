using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class PointTransactionRepository : IPointTransactionRepository
{
    private readonly MongoDbContext _context;

    public PointTransactionRepository(MongoDbContext context) => _context = context;

    public async Task<PointTransaction> CreateAsync(PointTransaction transaction)
    {
        await _context.PointTransactions.InsertOneAsync(transaction);
        return transaction;
    }

    // Fonte da verdade: soma de todos os deltas. balanceAfter em cada doc e so um snapshot de leitura rapida.
    //
    // Antes carregava TODAS as transacoes da familia pra memoria pra somar em C#
    // (revisao de melhorias, 2026-09-10) -- uma familia com anos de historico ia
    // trazer milhares de documentos so pra calcular um numero, numa chamada que
    // acontece a cada conclusao de tarefa e a cada consulta de saldo. Agrega no
    // proprio Mongo ($match + $group/$sum): so o total viaja pela rede.
    public async Task<int> GetBalanceAsync(ObjectId userId)
    {
        var result = await _context.PointTransactions
            .Aggregate()
            .Match(t => t.FamilyId == userId)
            .Group(t => 1, g => new { Total = g.Sum(t => t.Points) })
            .FirstOrDefaultAsync();

        return result?.Total ?? 0;
    }

    // Paginado (achado #4 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md):
    // antes tinha um limit=100 fixo sem jeito de ver o resto do extrato. TotalCount vem de
    // uma segunda query (CountDocumentsAsync) -- o Mongo nao devolve isso de graca junto
    // com Skip/Limit.
    public async Task<(List<PointTransaction> Items, long TotalCount)> GetHistoryAsync(
        ObjectId userId, int page, int pageSize)
    {
        var filter = Builders<PointTransaction>.Filter.Eq(t => t.FamilyId, userId);

        var totalCount = await _context.PointTransactions.CountDocumentsAsync(filter);
        var items = await _context.PointTransactions.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<List<PointTransaction>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.PointTransactions.Find(t => t.FamilyId == familyId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.PointTransactions.DeleteManyAsync(t => t.FamilyId == familyId);
}
