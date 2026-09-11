using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 6: mostra
// evolucao de independencia (quantas tarefas a crianca comecou sozinha, quantas
// precisaram de uma sugestao do PACUS, quantas precisaram de lembrete de adulto)
// nos ultimos 7 dias operacionais comparado aos 7 anteriores -- nunca so "quantas
// tarefas concluiu".
public class AutonomyService : IAutonomyService
{
    // Ate 14 dias cabem folgadamente no limite de pageSize do PaginationHelper (100).
    private const int WindowDays = 7;

    private readonly IDailyRoutineRepository _dailyRoutineRepository;

    public AutonomyService(IDailyRoutineRepository dailyRoutineRepository)
    {
        _dailyRoutineRepository = dailyRoutineRepository;
    }

    public async Task<AutonomyWeeklyReportResponse> GetWeeklyReportAsync(
        ObjectId familyId, string timezone, DateTime? utcNow = null)
    {
        var today = TimezoneHelper.GetOperationalDate(timezone, utcNow);
        var fromDate = TimezoneHelper.AddDays(today, -(WindowDays - 1));
        var previousFromDate = TimezoneHelper.AddDays(fromDate, -WindowDays);
        var yesterday = TimezoneHelper.AddDays(today, -1);

        // GetHistoryAsync so devolve dias ja fechados (RoutineStatus.Closed) -- a
        // rotina de hoje ainda esta aberta na maior parte do dia, entao precisa ser
        // buscada a parte pra nao ficar de fora do relatorio ate o fechamento do dia.
        var (closedRoutines, _) = await _dailyRoutineRepository.GetHistoryAsync(
            familyId, previousFromDate, yesterday, page: 1, pageSize: WindowDays * 2 + 2);

        var todayRoutine = await _dailyRoutineRepository.GetByUserAndDateAsync(familyId, today);

        var routines = todayRoutine is null
            ? closedRoutines
            : closedRoutines.Append(todayRoutine).ToList();

        var currentWindowTasks = routines
            .Where(r => !TimezoneHelper.IsBefore(r.Date, fromDate))
            .SelectMany(r => r.Tasks)
            .Where(t => t.DeletedAt is null)
            .ToList();

        var previousWindowTasks = routines
            .Where(r => TimezoneHelper.IsBefore(r.Date, fromDate) && !TimezoneHelper.IsBefore(r.Date, previousFromDate))
            .SelectMany(r => r.Tasks)
            .Where(t => t.DeletedAt is null)
            .ToList();

        return new AutonomyWeeklyReportResponse(
            fromDate,
            today,
            CountByInitiative(currentWindowTasks, TaskInitiativeLevel.SelfStarted),
            CountByInitiative(currentWindowTasks, TaskInitiativeLevel.PromptedByPacus),
            CountByInitiative(currentWindowTasks, TaskInitiativeLevel.PromptedByAdult),
            currentWindowTasks.Count(t => t.Initiative is null),
            CountByInitiative(previousWindowTasks, TaskInitiativeLevel.SelfStarted),
            CountByInitiative(previousWindowTasks, TaskInitiativeLevel.PromptedByPacus),
            CountByInitiative(previousWindowTasks, TaskInitiativeLevel.PromptedByAdult));
    }

    private static int CountByInitiative(
        List<Pacus.Domain.Entities.DailyTask> tasks, TaskInitiativeLevel level) =>
        tasks.Count(t => t.Initiative == level);
}

