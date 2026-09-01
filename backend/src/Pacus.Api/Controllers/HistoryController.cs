using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;

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

    // Respostas usam .ToResponse() (DailyRoutineDto.cs) em vez de devolver a
    // entidade de dominio crua (achado #3 da auditoria de API de 2026-09-01 --
    // ver docs/ESTADO_ATUAL.md). A listagem por periodo (sem "date") e paginada
    // (achado #4 da mesma auditoria): cresce um dia a mais pra sempre, entao
    // devolver tudo de uma vez nao escala.
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? date,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationHelper.DefaultPageSize)
    {
        if (!string.IsNullOrEmpty(date))
        {
            var routine = await _dailyRoutineRepository.GetByUserAndDateAsync(_currentUser.FamilyId, date);
            return routine is null ? NotFound() : Ok(routine.ToResponse());
        }

        PaginationHelper.Validate(page, pageSize);

        var (items, totalCount) = await _dailyRoutineRepository.GetHistoryAsync(
            _currentUser.FamilyId, from, to, page, pageSize);

        return Ok(new PagedResult<DailyRoutineResponse>(
            items.Select(r => r.ToResponse()).ToList(), page, pageSize, totalCount));
    }
}
