using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly MongoDbContext _context;

    public StoreRepository(MongoDbContext context) => _context = context;

    public Task<List<StoreItem>> GetActiveItemsAsync(ObjectId userId) =>
        _context.StoreItems.Find(i => i.FamilyId == userId && i.Active)
            .SortBy(i => i.Cost)
            .ToListAsync();

    public Task<StoreItem?> GetItemByIdAsync(ObjectId id) =>
        _context.StoreItems.Find(i => i.Id == id).FirstOrDefaultAsync();

    public async Task<StoreItem> CreateItemAsync(StoreItem item)
    {
        await _context.StoreItems.InsertOneAsync(item);
        return item;
    }

    public Task UpdateItemAsync(StoreItem item) =>
        _context.StoreItems.ReplaceOneAsync(i => i.Id == item.Id, item);

    public Task<Redemption?> GetRedemptionByIdAsync(ObjectId id) =>
        _context.Redemptions.Find(r => r.Id == id).FirstOrDefaultAsync();

    public async Task<Redemption> CreateRedemptionAsync(Redemption redemption)
    {
        await _context.Redemptions.InsertOneAsync(redemption);
        return redemption;
    }

    public Task UpdateRedemptionAsync(Redemption redemption) =>
        _context.Redemptions.ReplaceOneAsync(r => r.Id == redemption.Id, redemption);

    public Task<List<StoreItem>> GetAllItemsByFamilyAsync(ObjectId familyId) =>
        _context.StoreItems.Find(i => i.FamilyId == familyId)
            .SortBy(i => i.Cost)
            .ToListAsync();

    public Task<List<Redemption>> GetAllRedemptionsByFamilyAsync(ObjectId familyId) =>
        _context.Redemptions.Find(r => r.FamilyId == familyId)
            .SortByDescending(r => r.RequestedAt)
            .ToListAsync();
}
