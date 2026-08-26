using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;

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
}
