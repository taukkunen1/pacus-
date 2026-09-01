using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;
    private readonly IPointsService _pointsService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly IFamilyTimezoneService _familyTimezoneService;

    public StoreService(
        IStoreRepository storeRepository,
        IPointsService pointsService,
        IAuditLogRepository auditLogRepository,
        IDailyRoutineService dailyRoutineService,
        IFamilyTimezoneService familyTimezoneService)
    {
        _storeRepository = storeRepository;
        _pointsService = pointsService;
        _auditLogRepository = auditLogRepository;
        _dailyRoutineService = dailyRoutineService;
        _familyTimezoneService = familyTimezoneService;
    }

    public async Task<StoreItem> CreateItemAsync(ObjectId familyId, ObjectId createdBy, CreateStoreItemRequest request)
    {
        if (request.DailyLimit is not null && request.DailyLimit <= 0)
            throw new ValidationException("O limite diario, quando informado, deve ser maior que zero.");

        if (request.ScreenTimeMinutes is not null && request.ScreenTimeMinutes <= 0)
            throw new ValidationException("Os minutos de tempo de tela, quando informados, devem ser maiores que zero.");

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
            DailyLimit = request.DailyLimit,
            ScreenTimeMinutes = request.ScreenTimeMinutes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await _storeRepository.CreateItemAsync(item);
    }

    // Edicao de item existente -- so os campos de conteudo/regra mudam; Active, CreatedBy,
    // CreatedAt e Stock (baixa automatica por resgate) nao sao tocados aqui.
    public async Task<StoreItem> UpdateItemAsync(ObjectId familyId, string itemId, CreateStoreItemRequest request)
    {
        var item = await GetOwnedItemAsync(familyId, itemId);

        if (request.DailyLimit is not null && request.DailyLimit <= 0)
            throw new ValidationException("O limite diario, quando informado, deve ser maior que zero.");

        if (request.ScreenTimeMinutes is not null && request.ScreenTimeMinutes <= 0)
            throw new ValidationException("Os minutos de tempo de tela, quando informados, devem ser maiores que zero.");

        item.Title = request.Title;
        item.Description = request.Description;
        item.Cost = request.Cost;
        item.Category = request.Category;
        item.Icon = request.Icon;
        item.Stock = request.Stock;
        item.DailyLimit = request.DailyLimit;
        item.ScreenTimeMinutes = request.ScreenTimeMinutes;
        item.UpdatedAt = DateTime.UtcNow;

        await _storeRepository.UpdateItemAsync(item);
        return item;
    }

    // Desativar em vez de apagar -- resgates ja feitos referenciam o item pelo id
    // (historico/auditoria), e RequestRedemptionAsync ja rejeita item com Active=false.
    public async Task<StoreItem> SetItemActiveAsync(ObjectId familyId, string itemId, bool active)
    {
        var item = await GetOwnedItemAsync(familyId, itemId);
        item.Active = active;
        item.UpdatedAt = DateTime.UtcNow;
        await _storeRepository.UpdateItemAsync(item);
        return item;
    }

    private async Task<StoreItem> GetOwnedItemAsync(ObjectId familyId, string itemId)
    {
        if (!ObjectId.TryParse(itemId, out var id))
            throw new ValidationException("Id de item da loja invalido.");

        var item = await _storeRepository.GetItemByIdAsync(id);
        if (item is null || item.FamilyId != familyId)
            throw new NotFoundException("Item da loja nao encontrado.");

        return item;
    }

    public async Task<Redemption> RequestRedemptionAsync(ObjectId familyId, ObjectId childId, ObjectId storeItemId)
    {
        var item = await _storeRepository.GetItemByIdAsync(storeItemId);
        if (item is null || item.FamilyId != familyId || !item.Active)
            throw new NotFoundException("Item da loja nao encontrado ou indisponivel.");

        if (item.Stock is not null && item.Stock <= 0)
            throw new ValidationException("Item sem estoque disponivel.");

        if (item.DailyLimit is not null)
            await EnsureDailyLimitNotReachedAsync(familyId, item);

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

    // Conta quantos resgates deste item ja existem no dia operacional de hoje (Rejected nao
    // conta -- um pedido negado nao deveria consumir a vaga do dia). Busca uma janela de 2 dias
    // em UTC (bem mais que qualquer fuso precisa) e filtra pelo dia operacional exato depois,
    // mesmo padrao usado no resto do backend (ver TimezoneHelper).
    private async Task EnsureDailyLimitNotReachedAsync(ObjectId familyId, StoreItem item)
    {
        var timezone = await _familyTimezoneService.GetTimezoneAsync(familyId);
        var today = TimezoneHelper.GetOperationalDate(timezone);
        var sinceUtc = DateTime.UtcNow.AddDays(-2);

        var recent = await _storeRepository.GetRedemptionsByItemSinceAsync(familyId, item.Id, sinceUtc);
        var usedToday = recent.Count(r =>
            r.Status != RedemptionStatus.Rejected &&
            TimezoneHelper.GetOperationalDate(timezone, r.RequestedAt) == today);

        if (usedToday >= item.DailyLimit)
            throw new ValidationException(
                $"Limite diario deste item ja atingido ({item.DailyLimit}x por dia). Tente novamente amanha.");
    }

    public async Task<Redemption> ApproveRedemptionAsync(ObjectId familyId, string redemptionId, ObjectId reviewedBy)
    {
        var redemption = await GetOwnedPendingRedemptionAsync(familyId, redemptionId);

        var balance = await _pointsService.GetBalanceAsync(familyId);
        if (balance < redemption.Cost)
            throw new ValidationException("Saldo de Pacus Points insuficiente para aprovar este resgate.");

        var item = await _storeRepository.GetItemByIdAsync(redemption.StoreItemId);
        var timezone = await _familyTimezoneService.GetTimezoneAsync(familyId);

        // Se o item concede tempo de tela, garante que a rotina de hoje existe ANTES de
        // debitar qualquer ponto — AdjustGameTimerAsync exige uma rotina em aberto e nao a
        // cria sozinho. Fazer isso primeiro evita o cenario de aprovar o resgate (debitar
        // pontos, marcar Approved) e so depois descobrir que o timer nao pode ser ajustado.
        if (item?.ScreenTimeMinutes is int minutes)
        {
            await _dailyRoutineService.GetOrCreateTodayAsync(familyId, timezone);
        }

        // Debita o saldo — gera a transacao ANTES de marcar aprovado, para que uma falha aqui
        // nao deixe a redemption "aprovada" sem o correspondente registro auditavel de gasto.
        var today = TimezoneHelper.GetOperationalDate(timezone);
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

        // Concede o tempo de tela comprado -- soma direto no game timer do dia (mesmo
        // mecanismo dos botoes +5/-5 min do adulto). actorRole "adult" porque aprovar
        // resgate ja e uma acao restrita a adulto (RequireRole no controller).
        if (item?.ScreenTimeMinutes is int screenTimeMinutes)
        {
            await _dailyRoutineService.AdjustGameTimerAsync(familyId, screenTimeMinutes, reviewedBy, "adult");
        }

        // Baixa de estoque para itens finitos (ex. o Hot Wheels tem 1 unidade).
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
        var screenTimeNote = item?.ScreenTimeMinutes is int grantedMinutes
            ? $", +{grantedMinutes}min de tempo de tela"
            : string.Empty;

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Action = "redemption.approved",
            EntityType = "Redemption",
            EntityId = redemption.Id.ToString(),
            Details = $"Resgate aprovado: {redemption.ItemTitle} ({redemption.Cost} pontos{screenTimeNote})",
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
            throw new ValidationException("Id de resgate invalido.");

        var redemption = await _storeRepository.GetRedemptionByIdAsync(id);
        if (redemption is null || redemption.FamilyId != familyId)
            throw new NotFoundException("Resgate nao encontrado.");

        if (redemption.Status != RedemptionStatus.Pending)
            throw new ConflictException("Este resgate ja foi revisado.");

        return redemption;
    }
}
