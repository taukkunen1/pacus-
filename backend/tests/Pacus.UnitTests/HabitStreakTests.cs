using MongoDB.Bson;
using Pacus.Application.Services;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cobre DailyRoutineService.ComputeHabitStreaksAsync -- modo habito consolidado
// (2026-09-28, ver docs/PROPOSITO.md e Lally et al. 2010): conta dias consecutivos
// fechados em que uma tarefa permanente (TaskTemplateId) apareceu concluida,
// retroagindo a partir do dia anterior ao da rotina passada.
public class HabitStreakTests
{
    private static (FakeDailyRoutineRepository routines, DailyRoutineService dailyRoutine)
        BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templateRepo = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        var dailyRoutine = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        return (routines, dailyRoutine);
    }

    private static DailyRoutine ClosedRoutine(
        ObjectId userId, string date, string templateId, TaskItemStatus status) => new()
    {
        Id = ObjectId.GenerateNewId(),
        FamilyId = userId,
        Date = date,
        Status = RoutineStatus.Closed,
        Tasks = new List<DailyTask>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                TaskTemplateId = templateId,
                Title = "Escovar os dentes",
                Type = TaskType.Expected,
                Period = TaskPeriod.Morning,
                Points = 1,
                Status = status,
            },
        },
    };

    private static DailyRoutine OpenRoutineWithTask(
        ObjectId userId, string date, string templateId) => new()
    {
        Id = ObjectId.GenerateNewId(),
        FamilyId = userId,
        Date = date,
        Status = RoutineStatus.Open,
        Tasks = new List<DailyTask>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                TaskTemplateId = templateId,
                Title = "Escovar os dentes",
                Type = TaskType.Expected,
                Period = TaskPeriod.Morning,
                Points = 1,
                Status = TaskItemStatus.Pending,
            },
        },
    };

    [Fact]
    public async Task ComputeHabitStreaksAsync_TarefaAvulsaSemTemplate_RetornaZeroSemConsultarHistorico()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var today = new DailyRoutine
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = userId,
            Date = "2026-09-28",
            Status = RoutineStatus.Open,
            Tasks = new List<DailyTask>
            {
                new() { Id = "avulsa-1", TaskTemplateId = null, Title = "Tarefa de hoje", Status = TaskItemStatus.Pending },
            },
        };

        var streaks = await dailyRoutine.ComputeHabitStreaksAsync(userId, today);

        Assert.Empty(streaks);
    }

    [Fact]
    public async Task ComputeHabitStreaksAsync_66DiasSeguidosConcluidos_AtingeLimiarDeConsolidacao()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        const string templateId = "tpl-escovar-dentes";

        // 70 dias fechados seguidos, todos concluidos, terminando em 2026-09-27
        // (o dia anterior ao "hoje" de 2026-09-28 usado abaixo).
        var cursor = new DateOnly(2026, 9, 27);
        for (var i = 0; i < 70; i++)
        {
            await routines.CreateAsync(
                ClosedRoutine(userId, cursor.ToString("yyyy-MM-dd"), templateId, TaskItemStatus.Done));
            cursor = cursor.AddDays(-1);
        }

        var today = OpenRoutineWithTask(userId, "2026-09-28", templateId);

        var streaks = await dailyRoutine.ComputeHabitStreaksAsync(userId, today);

        Assert.True(streaks[templateId] >= DailyRoutineService.HabitConsolidationDays);
    }

    [Fact]
    public async Task ComputeHabitStreaksAsync_DiaNaoConcluidoNoMeioDoHistorico_QuebraOStreakAli()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        const string templateId = "tpl-escovar-dentes";

        // 5 dias concluidos (27, 26, 25, 24, 23-set), depois um dia NAO concluido
        // (22-set) -- o streak deve parar em 5, ignorando qualquer coisa antes disso.
        var dates = new[] { "2026-09-27", "2026-09-26", "2026-09-25", "2026-09-24", "2026-09-23" };
        foreach (var date in dates)
        {
            await routines.CreateAsync(
                ClosedRoutine(userId, date, templateId, TaskItemStatus.Done));
        }
        await routines.CreateAsync(
            ClosedRoutine(userId, "2026-09-22", templateId, TaskItemStatus.Pending));
        await routines.CreateAsync(
            ClosedRoutine(userId, "2026-09-21", templateId, TaskItemStatus.Done));

        var today = OpenRoutineWithTask(userId, "2026-09-28", templateId);

        var streaks = await dailyRoutine.ComputeHabitStreaksAsync(userId, today);

        Assert.Equal(5, streaks[templateId]);
        Assert.True(streaks[templateId] < DailyRoutineService.HabitConsolidationDays);
    }

    [Fact]
    public async Task ComputeHabitStreaksAsync_SemHistorico_RetornaZeroParaOTemplate()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        const string templateId = "tpl-novo";

        var today = OpenRoutineWithTask(userId, "2026-09-28", templateId);

        var streaks = await dailyRoutine.ComputeHabitStreaksAsync(userId, today);

        Assert.Equal(0, streaks[templateId]);
    }
}
