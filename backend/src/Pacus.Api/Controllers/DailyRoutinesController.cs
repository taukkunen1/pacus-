using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Exceptions;
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

    // Sem try/catch aqui de proposito (nem nas actions abaixo): NotFoundException/
    // ConflictException/ValidationException/UnauthorizedAccessException lancadas pelo
    // service viram o status HTTP certo sozinhas, via
    // Pacus.Api.Middleware.AppExceptionHandler (achado #1 da auditoria de API de
    // 2026-09-01 -- ver docs/ESTADO_ATUAL.md). E as respostas usam .ToResponse()
    // (DailyRoutineDto.cs) em vez de devolver a entidade de dominio crua (achado #3
    // da mesma auditoria).

    // Tentativas extras so aqui: GetToday e uma leitura do ponto de vista do usuario,
    // mas por baixo pode escrever (fechar o dia anterior, sincronizar tarefas novas de
    // template) via concorrencia otimista (achado #5 da auditoria -- ver
    // docs/ESTADO_ATUAL.md e DailyRoutineRepository.UpdateAsync). Duas abas/dispositivos
    // da familia abrindo a tela "Hoje" quase ao mesmo tempo bastam pra colidir e um dos
    // dois tomar ConflictException (409) so por ter carregado a pagina -- nao por ter
    // pedido uma acao. Relendo e tentando de novo aqui, a proxima passada ja enxerga o
    // estado gravado pela primeira e normalmente nao ha mais nada a sincronizar, entao
    // resolve sozinho sem incomodar quem so estava abrindo o app.
    private const int GetTodayMaxAttempts = 3;

    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var familyId = _currentUser.FamilyId;
        var timezone = await _familyTimezoneService.GetTimezoneAsync(familyId);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // "Nao e necessario manter um processo rodando exatamente a meia-noite" — o
                // fechamento acontece de forma preguicosa, no primeiro acesso que perceber
                // que o dia virou.
                await _dayClosingService.CloseIfDueAsync(familyId, timezone);

                var routine = await _dailyRoutineService.GetOrCreateTodayAsync(familyId, timezone);
                return Ok(routine.ToResponse());
            }
            catch (ConflictException) when (attempt < GetTodayMaxAttempts)
            {
                // corrida passageira contra outra requisicao concorrente -- tenta de novo
                // com estado fresco. Na ultima tentativa deixa o ConflictException subir
                // pro AppExceptionHandler (409) normalmente: se colidiu 3x seguidas ja nao
                // e mais so azar de timing.
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetByDate([FromQuery] string date)
    {
        var routine = await _dailyRoutineRepository.GetByUserAndDateAsync(_currentUser.FamilyId, date);
        return routine is null ? NotFound() : Ok(routine.ToResponse());
    }

    // Reordenar e autonomia da crianca sobre o dia atual — sem RequireRole aqui de proposito.
    [HttpPut("today/order")]
    public async Task<IActionResult> UpdateOrder([FromBody] List<string> orderedTaskIds)
    {
        var routine = await _dailyRoutineService.ReorderTasksAsync(
            _currentUser.FamilyId, orderedTaskIds, _currentUser.UserId, _currentUser.Role.ToString());
        return Ok(routine.ToResponse());
    }

    // Pausar/despausar e acao de qualquer papel (adulto ou crianca) —
    // sem RequireRole aqui de proposito, igual o reorder acima.
    [HttpPut("today/game-timer/pause")]
    public async Task<IActionResult> PauseGameTimer()
    {
        var routine = await _dailyRoutineService.PauseGameTimerAsync(
            _currentUser.FamilyId, _currentUser.UserId, _currentUser.Role.ToString());
        return Ok(routine.ToResponse());
    }

    [HttpPut("today/game-timer/resume")]
    public async Task<IActionResult> ResumeGameTimer()
    {
        var routine = await _dailyRoutineService.ResumeGameTimerAsync(
            _currentUser.FamilyId, _currentUser.UserId, _currentUser.Role.ToString());
        return Ok(routine.ToResponse());
    }

    // +1h/-1h etc. Restrito ao painel adulto — a crianca recebe 403 direto
    // do RequireRole, nunca chega a bater no service.
    [RequireRole(UserRole.Adult)]
    [HttpPut("today/game-timer/adjust")]
    public async Task<IActionResult> AdjustGameTimer([FromBody] AdjustGameTimerRequest request)
    {
        var routine = await _dailyRoutineService.AdjustGameTimerAsync(
            _currentUser.FamilyId, request.DeltaMinutes, _currentUser.UserId, _currentUser.Role.ToString());
        return Ok(routine.ToResponse());
    }

    // Vinculo (relatedness -- ver docs/PROPOSITO.md): reacao pessoal do adulto sobre o
    // dia. Restrito ao painel adulto — a crianca recebe 403 direto do RequireRole,
    // nunca chega a bater no service (mesmo padrao do AdjustGameTimer acima).
    [RequireRole(UserRole.Adult)]
    [HttpPut("today/reaction")]
    public async Task<IActionResult> SetReaction([FromBody] SetDailyReactionRequest request)
    {
        var routine = await _dailyRoutineService.SetReactionAsync(
            _currentUser.FamilyId, request.Icon, request.Message, _currentUser.UserId, _currentUser.Role.ToString());
        return Ok(routine.ToResponse());
    }
}
