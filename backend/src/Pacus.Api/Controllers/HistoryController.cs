using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;

namespace Pacus.Api.Controllers;

// So leitura — nao ha endpoint de escrita no historico. Isso por si so ja cobre o
// cenario critico "crianca tentando alterar historico": nao existe caminho para tentar.
[ApiController]
[Authorize]
[Route("api/v1/history")]
public class HistoryController : ControllerBase
{
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly ICurrentUserService _currentUser;

    public HistoryController(IDailyRoutineRepository dailyRoutineRepository, ICurrentUserService currentUser)
    {
        _dailyRoutineRepository = dailyRoutineRepository;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? date, [FromQuery] string? from, [FromQuery] string? to)
    {
        if (!string.IsNullOrEmpty(date))
        {
            var routine = await _dailyRoutineRepository.GetByUserAndDateAsync(_currentUser.FamilyId, date);
            return routine is null ? NotFound() : Ok(routine);
        }

        var history = await _dailyRoutineRepository.GetHistoryAsync(_currentUser.FamilyId, from, to);
        return Ok(history);
    }
}
