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
    public async Task<int> GetBalanceAsync(ObjectId userId)
    {
        var filter = Builders<PointTransaction>.Filter.Eq(t => t.FamilyId, userId);
        var transactions = await _context.PointTransactions.Find(filter).ToListAsync();
        return transactions.Sum(t => t.Points);
    }

    public Task<List<PointTransaction>> GetHistoryAsync(ObjectId userId, int limit = 100) =>
        _context.PointTransactions.Find(t => t.FamilyId == userId)
            .SortByDescending(t => t.CreatedAt)
            .Limit(limit)
            .ToListAsync();
}
