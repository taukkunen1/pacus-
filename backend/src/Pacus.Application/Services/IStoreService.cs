using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Services;

public interface IStoreService
{
    Task<StoreItem> CreateItemAsync(ObjectId familyId, ObjectId createdBy, CreateStoreItemRequest request);

    // Edita um item existente da familia. Lanca InvalidOperationException se o item nao
    // existir/for de outra familia, ou se os limites informados forem invalidos.
    Task<StoreItem> UpdateItemAsync(ObjectId familyId, string itemId, CreateStoreItemRequest request);

    // Ativa/desativa (nunca apaga -- resgates antigos continuam referenciando o item).
    Task<StoreItem> SetItemActiveAsync(ObjectId familyId, string itemId, bool active);

    // Lanca InvalidOperationException se o item nao existe, estiver inativo, ou sem estoque.
    Task<Redemption> RequestRedemptionAsync(ObjectId familyId, ObjectId childId, ObjectId storeItemId);

    // Debita o saldo (gera PointTransaction tipo Redemption) e baixa estoque quando finito.
    // Lanca InvalidOperationException se saldo insuficiente ou a solicitacao ja tiver sido revisada.
    Task<Redemption> ApproveRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy);

    // Lanca InvalidOperationException se a solicitacao ja tiver sido revisada.
    Task<Redemption> RejectRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy);
}
