using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/daily-routines")]
public class DailyRoutinesController : ControllerBase
{
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly IDayClosingService _dayClosingService;
    private readonly ICurrentUserService _currentUser;

    public DailyRoutinesController(
        IDailyRoutineService dailyRoutineService,
        IDailyRoutineRepository dailyRoutineRepository,
        IDayClosingService dayClosingService,
        ICurrentUserService currentUser)
    {
        _dailyRoutineService = dailyRoutineService;
        _dailyRoutineRepository = dailyRoutineRepository;
        _dayClosingService = dayClosingService;
        _currentUser = currentUser;
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        // TODO: timezone devera vir de settings/perfil da familia, nao fixo.
        const string timezone = "America/Sao_Paulo";
        var familyId = _currentUser.FamilyId;

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
}
