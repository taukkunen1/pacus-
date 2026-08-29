using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;
    private readonly IPointsService _pointsService;
    private readonly IAuditLogRepository _auditLogRepository;

    // TODO: assim como o timezone nos outros services, isso deveria vir de settings
    // da familia em vez de fixo — ver mesmo TODO em DailyRoutinesController.
    private const string DefaultTimezone = "America/Sao_Paulo";

    public StoreService(
        IStoreRepository storeRepository,
        IPointsService pointsService,
        IAuditLogRepository auditLogRepository)
    {
        _storeRepository = storeRepository;
        _pointsService = pointsService;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<StoreItem> CreateItemAsync(ObjectId familyId, ObjectId createdBy, CreateStoreItemRequest request)
    {
        var item = new StoreItem
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Title = request.Title,
            Description = request.Description,
            Cost = request.Cost,
            Category = request.Category,
            Icon = request.Icon,
            Active = true,
            Stock = request.Stock,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await _storeRepository.CreateItemAsync(item);
    }

    public async Task<Redemption> RequestRedemptionAsync(ObjectId familyId, ObjectId childId, ObjectId storeItemId)
    {
        var item = await _storeRepository.GetItemByIdAsync(storeItemId);
        if (item is null || item.FamilyId != familyId || !item.Active)
            throw new InvalidOperationException("Item da loja nao encontrado ou indisponivel.");

        if (item.Stock is not null && item.Stock <= 0)
            throw new InvalidOperationException("Item sem estoque disponivel.");

        var redemption = new Redemption
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            StoreItemId = item.Id,
            ItemTitle = item.Title,
            Cost = item.Cost,
            Status = RedemptionStatus.Pending,
            RequestedBy = childId,
            RequestedAt = DateTime.UtcNow,
        };

        return await _storeRepository.CreateRedemptionAsync(redemption);
    }

    public async Task<Redemption> ApproveRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy)
    {
        var redemption = await GetOwnedPendingRedemptionAsync(familyId, redemptionId);

        var balance = await _pointsService.GetBalanceAsync(familyId);
        if (balance < redemption.Cost)
            throw new InvalidOperationException("Saldo de Pacus Points insuficiente para aprovar este resgate.");

        // Debita o saldo — gera a transacao ANTES de marcar aprovado, para que uma falha aqui
        // nao deixe a redemption "aprovada" sem o correspondente registro auditavel de gasto.
        var today = TimezoneHelper.GetOperationalDate(DefaultTimezone);
        await _pointsService.RecordAsync(
            familyId,
            dailyRoutineId: null,
            today,
            redemption.Id.ToString(),
            redemption.ItemTitle,
            PointTransactionType.Redemption,
            -redemption.Cost,
            reviewedBy,
            UserRole.Adult,
            reason: $"Resgate: {redemption.ItemTitle}");

        redemption.Status = RedemptionStatus.Approved;
        redemption.ReviewedBy = reviewedBy;
        redemption.ReviewedAt = DateTime.UtcNow;
        await _storeRepository.UpdateRedemptionAsync(redemption);

        // Baixa de estoque para itens finitos (ex. o Hot Wheels tem 1 unidade).
        var item = await _storeRepository.GetItemByIdAsync(redemption.StoreItemId);
        if (item is not null && item.Stock is not null)
        {
            item.Stock -= 1;
            if (item.Stock <= 0) item.Active = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _storeRepository.UpdateItemAsync(item);
        }

        // Log de auditoria (checklist de seguranca, item A5) — aprovar resgate e uma
        // acao administrativa sensivel (debita pontos da familia), registrada separada
        // do dado em si.
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Action = "redemption.approved",
            EntityType = "Redemption",
            EntityId = redemption.Id.ToString(),
            Details = $"Resgate aprovado: {redemption.ItemTitle} ({redemption.Cost} pontos)",
            ActorId = reviewedBy,
            ActorRole = UserRole.Adult,
            CreatedAt = DateTime.UtcNow,
        });

        return redemption;
    }

    public async Task<Redemption> RejectRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy)
    {
        var redemption = await GetOwnedPendingRedemptionAsync(familyId, redemptionId);

        redemption.Status = RedemptionStatus.Rejected;
        redemption.ReviewedBy = reviewedBy;
        redemption.ReviewedAt = DateTime.UtcNow;
        await _storeRepository.UpdateRedemptionAsync(redemption);

        // Log de auditoria (checklist de seguranca, item A5) — mesma logica do approve.
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Action = "redemption.rejected",
            EntityType = "Redemption",
            EntityId = redemption.Id.ToString(),
            Details = $"Resgate rejeitado: {redemption.ItemTitle} ({redemption.Cost} pontos)",
            ActorId = reviewedBy,
            ActorRole = UserRole.Adult,
            CreatedAt = DateTime.UtcNow,
        });

        return redemption;
    }

    private async Task<Redemption> GetOwnedPendingRedemptionAsync(ObjectId familyId, string redemptionId)
    {
        if (!ObjectId.TryParse(redemptionId, out var id))
            throw new InvalidOperationException("Id de resgate invalido.");

        var redemption = await _storeRepository.GetRedemptionByIdAsync(id);
        if (redemption is null || redemption.FamilyId != familyId)
            throw new InvalidOperationException("Resgate nao encontrado.");

        if (redemption.Status != RedemptionStatus.Pending)
            throw new InvalidOperationException("Este resgate ja foi revisado.");

        return redemption;
    }
}
