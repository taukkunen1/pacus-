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

    // Resgates deste item feitos desde `sinceUtc` (janela ampla o bastante para cobrir
    // qualquer fuso) -- StoreService filtra depois pelo dia operacional exato da familia,
    // igual ao padrao ja usado com TimezoneHelper no resto do backend.
    Task<List<Redemption>> GetRedemptionsByItemSinceAsync(ObjectId familyId, ObjectId storeItemId, DateTime sinceUtc);

    // Resgates Pending da familia, para a fila de aprovacao do adulto.
    Task<List<Redemption>> GetPendingRedemptionsByFamilyAsync(ObjectId familyId);

    // Todos os itens (ativos e inativos) e todos os resgates da familia, para
    // exportacao de dados (B2).
    Task<List<StoreItem>> GetAllItemsByFamilyAsync(ObjectId familyId);
    Task<List<Redemption>> GetAllRedemptionsByFamilyAsync(ObjectId familyId);

    // Remove todos os itens e resgates da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllItemsByFamilyAsync(ObjectId familyId);
    Task DeleteAllRedemptionsByFamilyAsync(ObjectId familyId);
}
