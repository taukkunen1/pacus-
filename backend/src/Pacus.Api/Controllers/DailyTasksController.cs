using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/daily-tasks")]
public class DailyTasksController : ControllerBase
{
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly ICurrentUserService _currentUser;

    public DailyTasksController(
        IDailyRoutineService dailyRoutineService,
        ICurrentUserService currentUser)
    {
        _dailyRoutineService = dailyRoutineService;
        _currentUser = currentUser;
    }

    // Sem try/catch aqui de proposito: NotFoundException/ConflictException/
    // ValidationException/UnauthorizedAccessException lancadas pelo service viram o
    // status HTTP certo sozinhas, via Pacus.Api.Middleware.AppExceptionHandler
    // (achado #1 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md).
    // E as respostas usam .ToResponse() (DailyRoutineDto.cs) em vez de devolver a
    // entidade de dominio crua (achado #3 da mesma auditoria).
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request)
    {
        var routine = await _dailyRoutineService.CreateAdHocTaskAsync(
            _currentUser.FamilyId,
            request,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(string id)
    {
        var routine = await _dailyRoutineService.ToggleTaskAsync(
            _currentUser.FamilyId,
            id,
            true,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpPost("{id}/reopen")]
    public async Task<IActionResult> Reopen(string id)
    {
        var routine = await _dailyRoutineService.ToggleTaskAsync(
            _currentUser.FamilyId,
            id,
            false,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpPut("{id}/option")]
    public async Task<IActionResult> SelectOption(
        string id,
        [FromBody] SelectTaskOptionRequest request)
    {
        var routine = await _dailyRoutineService.SelectTaskOptionAsync(
            _currentUser.FamilyId,
            id,
            request.SelectedOption,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpPut("{id}/points")]
    public async Task<IActionResult> AdjustPoints(
        string id,
        [FromBody] AdjustPointsRequest request)
    {
        var routine = await _dailyRoutineService.AdjustTaskPointsAsync(
            _currentUser.FamilyId,
            id,
            request.Points,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] DailyTaskUpdateRequest request)
    {
        var routine = await _dailyRoutineService.UpdateTaskAsync(
            _currentUser.FamilyId,
            id,
            request,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var routine = await _dailyRoutineService.DeleteTaskAsync(
            _currentUser.FamilyId,
            id,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md): "Como voce
    // comecou?" -- sem RequireRole aqui de proposito, e a propria crianca quem
    // autodeclara sua iniciativa.
    [HttpPut("{id}/initiative")]
    public async Task<IActionResult> SetInitiative(
        string id,
        [FromBody] SetTaskInitiativeRequest request)
    {
        var routine = await _dailyRoutineService.SetTaskInitiativeAsync(
            _currentUser.FamilyId,
            id,
            request.Initiative,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }

    // "O que aconteceu?" -- nunca usado pra punir (ver docs/ESTADO_ATUAL.md, "Nao
    // utilizar punicao"). Sem RequireRole aqui de proposito, mesma logica acima.
    [HttpPut("{id}/skip-reason")]
    public async Task<IActionResult> SetSkipReason(
        string id,
        [FromBody] SetTaskSkipReasonRequest request)
    {
        var routine = await _dailyRoutineService.SetTaskSkipReasonAsync(
            _currentUser.FamilyId,
            id,
            request.Reason,
            request.Note,
            _currentUser.UserId,
            _currentUser.Role.ToString());

        return Ok(routine.ToResponse());
    }
}
