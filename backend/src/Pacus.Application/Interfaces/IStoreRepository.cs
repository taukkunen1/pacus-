using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IStoreRepository
{
    Task<List<StoreItem>> GetActiveItemsAsync(ObjectId userId);
    Task<StoreItem?> GetItemByIdAsync(ObjectId id);
    Task<StoreItem> CreateItemAsync(StoreItem item);
    Task UpdateItemAsync(StoreItem item);
    Task<Redemption?> GetRedemptionByIdAsync(ObjectId id);
    Task<Redemption> CreateRedemptionAsync(Redemption redemption);
    Task UpdateRedemptionAsync(Redemption redemption);

    // Todos os itens (ativos e inativos) e todos os resgates da familia, para
    // exportacao de dados (B2).
    Task<List<StoreItem>> GetAllItemsByFamilyAsync(ObjectId familyId);
    Task<List<Redemption>> GetAllRedemptionsByFamilyAsync(ObjectId familyId);
}
