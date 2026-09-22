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

        var currentWindowRoutines = routines
            .Where(r => !TimezoneHelper.IsBefore(r.Date, fromDate))
            .ToList();
        var previousWindowRoutines = routines
            .Where(r => TimezoneHelper.IsBefore(r.Date, fromDate) &&
                        !TimezoneHelper.IsBefore(r.Date, previousFromDate))
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
            CountByInitiative(previousWindowTasks, TaskInitiativeLevel.PromptedByAdult),
            currentWindowTasks.Count(t => t.CreatedByMember),
            currentWindowRoutines.Count(r => r.TomorrowPlanConfirmedAt is not null),
            previousWindowTasks.Count(t => t.CreatedByMember),
            previousWindowRoutines.Count(r => r.TomorrowPlanConfirmedAt is not null));
    }

    public async Task<List<RoutineSuggestionResponse>> GetRoutineSuggestionsAsync(ObjectId familyId)
    {
        var routines = (await _dailyRoutineRepository.GetAllByFamilyAsync(familyId))
            .Where(r => r.Status != RoutineStatus.Planned)
            .OrderByDescending(r => r.Date)
            .Take(30)
            .ToList();

        var suggestions = new List<RoutineSuggestionResponse>();
        if (routines.Count == 0)
            return suggestions;

        var tasks = routines
            .SelectMany(r => r.Tasks)
            .Where(t => t.DeletedAt is null)
            .ToList();

        var ownTasks = tasks.Where(t => t.CreatedByMember).ToList();

        var repeatedOwnIdea = ownTasks
            .GroupBy(t => t.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (repeatedOwnIdea is not null)
        {
            var title = repeatedOwnIdea.First().Title;
            suggestions.Add(new RoutineSuggestionResponse(
                "repeat-own-" + Slug(title),
                "own-idea",
                "Uma ideia que você costuma escolher",
                $"Você já colocou \"{title}\" em mais de um dia. Quer usar essa ideia de novo?",
                SuggestedTitle: title));
        }

        var preferredPeriod = ownTasks
            .GroupBy(t => t.Period)
            .OrderByDescending(g => g.Count())
            .Select(g => (TaskPeriod?)g.Key)
            .FirstOrDefault();

        var templateStats = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.TaskTemplateId))
            .GroupBy(t => t.TaskTemplateId!)
            .Where(g => g.Count() >= 3)
            .Select(g => new
            {
                TemplateId = g.Key,
                Title = g.Last().Title,
                Period = g.Last().Period,
                CompletionRate = g.Count(t => t.Status == TaskItemStatus.Done) / (double)g.Count(),
            })
            .OrderBy(x => x.CompletionRate)
            .ToList();

        var difficult = templateStats.FirstOrDefault(x => x.CompletionRate < 0.60);
        if (difficult is not null && preferredPeriod is not null && preferredPeriod != difficult.Period)
        {
            suggestions.Add(new RoutineSuggestionResponse(
                "move-" + difficult.TemplateId,
                "routine-change",
                "Talvez outro horário funcione melhor",
                $"\"{difficult.Title}\" tem sido difícil de concluir. Você costuma escolher mais coisas {PeriodLabel(preferredPeriod.Value)}. Quer mover essa tarefa?",
                difficult.TemplateId,
                preferredPeriod.Value.ToString()));
        }

        var selfStarted = tasks
            .Where(t => t.Initiative == TaskInitiativeLevel.SelfStarted)
            .GroupBy(t => t.Period)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (selfStarted is not null)
        {
            suggestions.Add(new RoutineSuggestionResponse(
                "self-start-" + selfStarted.Key.ToString().ToLowerInvariant(),
                "insight",
                "Seu período mais independente",
                $"Nos últimos dias você começou mais tarefas por conta própria {PeriodLabel(selfStarted.Key)}."));
        }

        var lowCompletion = templateStats
            .Where(x => x.CompletionRate < 0.50)
            .Skip(difficult is null ? 0 : 1)
            .FirstOrDefault();

        if (lowCompletion is not null)
        {
            suggestions.Add(new RoutineSuggestionResponse(
                "simplify-" + lowCompletion.TemplateId,
                "routine-change",
                "Talvez essa tarefa possa ficar mais simples",
                $"\"{lowCompletion.Title}\" ficou pendente em vários dias. Você pode editar o nome, a descrição ou o horário para deixá-la mais fácil de começar.",
                lowCompletion.TemplateId));
        }

        return suggestions.Take(4).ToList();
    }

    private static string PeriodLabel(TaskPeriod period) => period switch
    {
        TaskPeriod.Morning => "de manhã",
        TaskPeriod.Afternoon => "à tarde",
        TaskPeriod.Evening => "à noite",
        _ => "em outro horário",
    };

    private static string Slug(string value) =>
        string.Concat(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');

    private static int CountByInitiative(
        List<Pacus.Domain.Entities.DailyTask> tasks, TaskInitiativeLevel level) =>
        tasks.Count(t => t.Initiative == level);
}

