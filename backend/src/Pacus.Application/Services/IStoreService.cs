using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Services;

public interface IStoreService
{
    Task<StoreItem> CreateItemAsync(ObjectId familyId, ObjectId createdBy, CreateStoreItemRequest request);

    // Lanca InvalidOperationException se o item nao existe, estiver inativo, ou sem estoque.
    Task<Redemption> RequestRedemptionAsync(ObjectId familyId, ObjectId childId, ObjectId storeItemId);

    // Debita o saldo (gera PointTransaction tipo Redemption) e baixa estoque quando finito.
    // Lanca InvalidOperationException se saldo insuficiente ou a solicitacao ja tiver sido revisada.
    Task<Redemption> ApproveRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy);

    // Lanca InvalidOperationException se a solicitacao ja tiver sido revisada.
    Task<Redemption> RejectRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy);
}
