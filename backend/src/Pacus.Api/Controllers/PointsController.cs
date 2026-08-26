using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Application.Utils;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/points")]
public class PointsController : ControllerBase
{
    private readonly IPointsService _pointsService;
    private readonly IPointTransactionRepository _pointTransactionRepository;
    private readonly ICurrentUserService _currentUser;

    public PointsController(
        IPointsService pointsService,
        IPointTransactionRepository pointTransactionRepository,
        ICurrentUserService currentUser)
    {
        _pointsService = pointsService;
        _pointTransactionRepository = pointTransactionRepository;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetBalance()
    {
        var balance = await _pointsService.GetBalanceAsync(_currentUser.FamilyId);
        return Ok(new { balance, brl = balance * 0.05 });
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
            const string timezone = "America/Sao_Paulo";
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
        }

        var newBalance = await _pointsService.GetBalanceAsync(_currentUser.FamilyId);
        return Ok(new { balance = newBalance, brl = newBalance * 0.05 });
    }
}
