using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/daily-routines")]
public class DailyRoutinesController : ControllerBase
{
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly IDayClosingService _dayClosingService;
    private readonly IFamilyTimezoneService _familyTimezoneService;
    private readonly ICurrentUserService _currentUser;

    public DailyRoutinesController(
        IDailyRoutineService dailyRoutineService,
        IDailyRoutineRepository dailyRoutineRepository,
        IDayClosingService dayClosingService,
        IFamilyTimezoneService familyTimezoneService,
        ICurrentUserService currentUser)
    {
        _dailyRoutineService = dailyRoutineService;
        _dailyRoutineRepository = dailyRoutineRepository;
        _dayClosingService = dayClosingService;
        _familyTimezoneService = familyTimezoneService;
        _currentUser = currentUser;
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var familyId = _currentUser.FamilyId;
        var timezone = await _familyTimezoneService.GetTimezoneAsync(familyId);

        // "Nao e necessario manter um processo rodando exatamente a meia-noite" — o fechamento
        // acontece de forma preguicosa, no primeiro acesso que perceber que o dia virou.
        await _dayClosingService.CloseIfDueAsync(familyId, timezone);

        var routine = await _dailyRoutineService.GetOrCreateTodayAsync(familyId, timezone);
        return Ok(routine);
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate([FromQuery] string date)
    {
        var routine = await _dailyRoutineRepository.GetByUserAndDateAsync(_currentUser.FamilyId, date);
        return routine is null ? NotFound() : Ok(routine);
    }

    // Reordenar e autonomia da crianca sobre o dia atual — sem RequireRole aqui de proposito.
    [HttpPut("today/order")]
    public async Task<IActionResult> UpdateOrder([FromBody] List<string> orderedTaskIds)
    {
        try
        {
            var routine = await _dailyRoutineService.ReorderTasksAsync(
                _currentUser.FamilyId, orderedTaskIds, _currentUser.UserId, _currentUser.Role.ToString());
            return Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Pausar/despausar e acao de qualquer papel (adulto ou crianca) —
    // sem RequireRole aqui de proposito, igual o reorder acima.
    [HttpPut("today/game-timer/pause")]
    public async Task<IActionResult> PauseGameTimer()
    {
        try
        {
            var routine = await _dailyRoutineService.PauseGameTimerAsync(
                _currentUser.FamilyId, _currentUser.UserId, _currentUser.Role.ToString());
            return Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("today/game-timer/resume")]
    public async Task<IActionResult> ResumeGameTimer()
    {
        try
        {
            var routine = await _dailyRoutineService.ResumeGameTimerAsync(
                _currentUser.FamilyId, _currentUser.UserId, _currentUser.Role.ToString());
            return Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // +1h/-1h etc. Restrito ao painel adulto — a crianca recebe 403 direto
    // do RequireRole, nunca chega a bater no service.
    [RequireRole(UserRole.Adult)]
    [HttpPut("today/game-timer/adjust")]
    public async Task<IActionResult> AdjustGameTimer([FromBody] AdjustGameTimerRequest request)
    {
        try
        {
            var routine = await _dailyRoutineService.AdjustGameTimerAsync(
                _currentUser.FamilyId, request.DeltaMinutes, _currentUser.UserId, _currentUser.Role.ToString());
            return Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    // Vinculo (relatedness -- ver docs/PROPOSITO.md): reacao pessoal do adulto sobre o
    // dia. Restrito ao painel adulto — a crianca recebe 403 direto do RequireRole,
    // nunca chega a bater no service (mesmo padrao do AdjustGameTimer acima).
    [RequireRole(UserRole.Adult)]
    [HttpPut("today/reaction")]
    public async Task<IActionResult> SetReaction([FromBody] SetDailyReactionRequest request)
    {
        try
        {
            var routine = await _dailyRoutineService.SetReactionAsync(
                _currentUser.FamilyId, request.Icon, request.Message, _currentUser.UserId, _currentUser.Role.ToString());
            return Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
