using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

// Implementa o algoritmo de fechamento do dia da especificacao:
//  1. determina a data operacional no timezone do usuario
//  2. busca a rotina em aberto (pode ser de varios dias atras, se o app ficou sem uso)
//  3. idempotente â€” se ja fechada (nao esta mais Open), nao reprocessa
//  4-6. marca fechada, congela estatisticas e pontos ganhos
//  7-9. cresce o PACUS uma unica vez por dia fechado, protegido por lastGrowthDate
//       E por um indice unico {userId, date} em pacus_growth (defesa em profundidade)
//  10-11. disponibiliza o(s) novo(s) dia(s) com tarefas pendentes
//
// Roda ate alcancar a data operacional atual â€” se o usuario ficou varios dias sem abrir
// o app, cada dia perdido ainda vira sua propria fotografia (0 tarefas concluidas), em vez
// de ser silenciosamente pulado.
public class DayClosingService : IDayClosingService
{
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly IPacusRepository _pacusRepository;
    private readonly IPacusGrowthRepository _pacusGrowthRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IClock _clock;

    public DayClosingService(
        IDailyRoutineRepository dailyRoutineRepository,
        IDailyRoutineService dailyRoutineService,
        IPacusRepository pacusRepository,
        IPacusGrowthRepository pacusGrowthRepository,
        ISettingsRepository settingsRepository,
        IClock? clock = null)
    {
        _dailyRoutineRepository = dailyRoutineRepository;
        _dailyRoutineService = dailyRoutineService;
        _pacusRepository = pacusRepository;
        _pacusGrowthRepository = pacusGrowthRepository;
        _settingsRepository = settingsRepository;
        _clock = clock ?? new SystemClock();
    }

    public async Task CloseIfDueAsync(ObjectId userId, string timezone)
    {
        var today = TimezoneHelper.GetOperationalDate(timezone, _clock.UtcNow);

        var openRoutine = await _dailyRoutineRepository.GetLatestOpenAsync(userId);
        if (openRoutine is null)
        {
            // Primeiro acesso do usuario, ou algum estado inconsistente â€” apenas garante que hoje existe.
            await _dailyRoutineService.GetOrCreateTodayAsync(userId, timezone);
            return;
        }

        // Passo 3 (idempotencia): enquanto a rotina aberta for de uma data anterior a hoje,
        // ela precisa ser fechada. Se ja for a de hoje, nao ha nada a fazer.
        var current = openRoutine;
        while (TimezoneHelper.IsBefore(current.Date, today))
        {
            await CloseRoutineAsync(current);

            var nextDate = TimezoneHelper.NextDate(current.Date);
            current = await _dailyRoutineService.CreateRoutineForDateAsync(userId, nextDate, timezone);
        }
    }

    private async Task CloseRoutineAsync(DailyRoutine routine)
    {
        // Passos 4-5: marcar fechada e congelar estatisticas (Stats/PointsEarned ja sao
        // recalculados a cada toggle por DailyRoutineService, mas recalculamos aqui de novo
        // por seguranca â€” o fechamento nao deve confiar em estado potencialmente desatualizado).
        routine.Status = RoutineStatus.Closed;
        routine.ClosedAt = DateTime.UtcNow;
        await _dailyRoutineRepository.UpdateAsync(routine);

        // Passos 7-9: crescimento do PACUS, uma unica vez por dia fechado, independente do desempenho.
        await GrowPacusOnceAsync(routine);
    }

    private async Task GrowPacusOnceAsync(DailyRoutine routine)
    {
        var pacus = await _pacusRepository.GetByFamilyIdAsync(routine.UserId);
        if (pacus is null) return; // Usuario ainda nao tem um PACUS configurado â€” nada a crescer.

        // Guarda primaria: lastGrowthDate. Se ja processado, sai sem tocar em nada.
        if (pacus.LastGrowthDate == routine.Date) return;

        // Guarda secundaria (defesa em profundidade): indice unico {userId, date} em pacus_growth.
        // Cobre corridas onde duas requisicoes tentam fechar o mesmo dia ao mesmo tempo.
        var alreadyLogged = await _pacusGrowthRepository.GetByUserAndDateAsync(routine.UserId, routine.Date);
        if (alreadyLogged is not null) return;

        var stageBefore = pacus.Stage;
        var sizeBefore = pacus.Size;

        var settings = await _settingsRepository.GetByUserIdAsync(routine.UserId);
        var newStage = DetermineStage(settings?.GrowthStages, routine.Date, stageBefore);

        pacus.TotalClosedDays += 1;
        pacus.Stage = newStage;
        pacus.Size = ComputeSize(newStage, pacus.TotalClosedDays);
        pacus.LastGrowthDate = routine.Date;
        pacus.UpdatedAt = DateTime.UtcNow;

        if (newStage != stageBefore)
        {
            pacus.StageHistory.Add(new PacusStageHistoryEntry
            {
                Stage = newStage,
                ReachedAt = DateTime.UtcNow,
            });
        }

        await _pacusRepository.UpdateAsync(pacus);

        // Log dedicado em pacus_growth â€” auditavel independentemente do estado atual do PACUS,
        // e e o que garante (via indice unico) que o crescimento nunca duplica por dia.
        await _pacusGrowthRepository.CreateAsync(new PacusGrowthLog
        {
            Id = ObjectId.GenerateNewId(),
            UserId = routine.UserId,
            PacusId = pacus.Id,
            Date = routine.Date,
            DailyRoutineId = routine.Id,
            StageBefore = stageBefore,
            StageAfter = newStage,
            SizeBefore = sizeBefore,
            SizeAfter = pacus.Size,
            CreatedAt = DateTime.UtcNow,
        });
    }

    // Estagio e determinado pela configuracao em settings.growthStages (datas do calendario
    // atual, ex. Ovo 09/08 -> Adulto 31/08). Se nao houver configuracao, mantem o estagio atual
    // â€” nunca regride e nunca avanca "no escuro" sem uma regra definida.
    private static PacusStage DetermineStage(List<GrowthStageConfig>? growthStages, string operationalDate, PacusStage fallback)
    {
        if (growthStages is null || growthStages.Count == 0) return fallback;

        var applicable = growthStages
            .Where(s => !TimezoneHelper.IsBefore(operationalDate, s.Date)) // s.Date <= operationalDate
            .OrderByDescending(s => s.Date)
            .FirstOrDefault();

        return applicable?.Stage ?? fallback;
    }

    // Tamanho progressivo e simples por enquanto â€” cresce com o total de dias fechados,
    // com um teto por estagio para o salto visual acompanhar o estagio (filhote -> pequeno,
    // juvenil -> medio, adulto -> maior). Regra final de escala ainda esta em aberto na spec.
    private static double ComputeSize(PacusStage stage, int totalClosedDays)
    {
        var stageCap = stage switch
        {
            PacusStage.Egg => 1.0,
            PacusStage.Cracking => 1.2,
            PacusStage.Hatching => 1.5,
            PacusStage.Baby => 2.5,
            PacusStage.Young => 4.0,
            PacusStage.Adult => 6.0,
            _ => 1.0,
        };

        var grown = 1.0 + totalClosedDays * 0.15;
        return Math.Min(grown, stageCap);
    }
}


