using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/points")]
public class PointsController : ControllerBase
{
    private readonly IPointsService _pointsService;
    private readonly IPointTransactionRepository _pointTransactionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IFamilyTimezoneService _familyTimezoneService;
    private readonly ICurrentUserService _currentUser;

    public PointsController(
        IPointsService pointsService,
        IPointTransactionRepository pointTransactionRepository,
        IAuditLogRepository auditLogRepository,
        ISettingsRepository settingsRepository,
        IFamilyTimezoneService familyTimezoneService,
        ICurrentUserService currentUser)
    {
        _pointsService = pointsService;
        _pointTransactionRepository = pointTransactionRepository;
        _auditLogRepository = auditLogRepository;
        _settingsRepository = settingsRepository;
        _familyTimezoneService = familyTimezoneService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetBalance()
    {
        var balance = await _pointsService.GetBalanceAsync(_currentUser.FamilyId);
        var rate = await GetPointToBrlRateAsync();
        return Ok(new { balance, brl = balance * rate });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        var transactions = await _pointTransactionRepository.GetHistoryAsync(_currentUser.FamilyId);
        return Ok(transactions);
    }

    // Define o saldo para um valor absoluto — pensado para migrar um saldo que a familia
    // ja tinha antes deste app (ex. vindo da versao anterior do tracker). Por baixo cria
    // uma unica transacao Adjustment com o delta necessario, entao o extrato continua
    // auditavel em vez de "aparecer do nada".
    [RequireRole(UserRole.Adult)]
    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustBalance([FromBody] SetPointsBalanceRequest request)
    {
        var currentBalance = await _pointsService.GetBalanceAsync(_currentUser.FamilyId);
        var delta = request.Balance - currentBalance;

        if (delta != 0)
        {
            var timezone = await _familyTimezoneService.GetTimezoneAsync(_currentUser.FamilyId);
            var date = TimezoneHelper.GetOperationalDate(timezone);

            await _pointsService.RecordAsync(
                _currentUser.FamilyId,
                dailyRoutineId: null,
                date: date,
                taskId: "manual-adjustment",
                taskTitle: "Ajuste manual de saldo",
                type: PointTransactionType.Adjustment,
                points: delta,
                actorId: _currentUser.UserId,
                actorRole: _currentUser.Role,
                reason: request.Reason ?? "Ajuste manual de saldo (migracao de progresso anterior)");

            // Log de auditoria (checklist de seguranca, item A5) — ajuste manual de
            // saldo e uma acao administrativa sensivel, registrada separada da
            // propria transacao de pontos (que ja existe em point_transactions).
            await _auditLogRepository.CreateAsync(new Pacus.Domain.Entities.AuditLog
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                Action = "points.manual_adjustment",
                EntityType = "PointsBalance",
                EntityId = _currentUser.FamilyId.ToString(),
                Details = $"Saldo: {currentBalance} -> {request.Balance} (delta {delta}, motivo: {request.Reason ?? "nao informado"})",
                ActorId = _currentUser.UserId,
                ActorRole = _currentUser.Role,
                CreatedAt = DateTime.UtcNow,
            });
        }

        var newBalance = await _pointsService.GetBalanceAsync(_currentUser.FamilyId);
        var rate = await GetPointToBrlRateAsync();
        return Ok(new { balance = newBalance, brl = newBalance * rate });
    }

    // Antes este valor estava fixo (0.05) direto nos dois endpoints acima, ignorando
    // Settings.PointToBrlRate por completo -- ou seja, mudar a taxa aqui nunca refletia
    // no saldo em R$ mostrado pro usuario. Corrigido: le a taxa configurada da familia,
    // com o mesmo fallback (Settings.DefaultPointToBrlRate) usado quando ainda nao existe
    // um documento de Settings salvo.
    //
    // Segunda parte do problema: famílias cujo Settings ja existia no Mongo antes dessa
    // mudanca (ex.: por terem ligado o tempo de jogo em algum momento) ficaram com
    // PointToBrlRate = 0.05 gravado no documento -- valor que nunca foi escolhido por
    // ninguem (nao existe endpoint pra configurar essa taxa), so o default antigo da
    // propriedade C# congelado no banco. Por isso curamos aqui: se o valor salvo for
    // exatamente a taxa antiga, tratamos como "nao migrado", aplicamos e persistimos o
    // default atual -- assim o proximo GetBalance ja vem certo sem precisar de migracao
    // manual no banco de producao.
    private async Task<double> GetPointToBrlRateAsync()
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId);
        if (settings is null) return Settings.DefaultPointToBrlRate;

        if (settings.PointToBrlRate == Settings.LegacyDefaultPointToBrlRate)
        {
            settings.PointToBrlRate = Settings.DefaultPointToBrlRate;
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.UpsertAsync(settings);
        }

        return settings.PointToBrlRate;
    }
}
