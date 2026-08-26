using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.Domain.Entities;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cenario critico #6 da spec (tarefa adicionada) + o requisito de que toda tarefa
// ad-hoc sempre tem um caminho de ser replicada em outro dia (template inativo).
public class AdHocTaskTests
{
    private static (DailyRoutineService dailyRoutine, FakeDailyRoutineRepository routines,
        FakeTaskTemplateRepository templates, FakeTaskEventRepository events)
        BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templates = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        var dailyRoutine = new DailyRoutineService(routines, templates, events, pointsService, new FakeSettingsRepository());
        return (dailyRoutine, routines, templates, events);
    }

    [Fact]
    public async Task CriancaCriaTarefaNova_EntraNaRotinaDeHojeComPontosPropostosPelaCrianca()
    {
        var (dailyRoutine, routines, _, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var request = new CreateTaskRequest("Comprar racao", null, "challenge", "afternoon", 3);
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId, request, userId, "child");

        var created = Assert.Single(routine.Tasks);
        Assert.Equal("Comprar racao", created.Title);
        Assert.Equal(3, created.Points);
        Assert.Equal("child", created.Origin);

        var saved = await routines.GetByUserAndDateAsync(userId, "2026-08-24");
        Assert.Single(saved!.Tasks);
    }

    [Fact]
    public async Task TarefaAdHoc_SempreCriaTemplateInativoComOsMesmosDados()
    {
        var (dailyRoutine, _, templates, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var request = new CreateTaskRequest("Fazer desenho", "capricho livre", "challenge", "evening", 2);
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId, request, userId, "child");
        var task = routine.Tasks[0];

        Assert.NotNull(task.TaskTemplateId);
        var template = await templates.GetByIdAsync(ObjectId.Parse(task.TaskTemplateId!));

        Assert.NotNull(template);
        Assert.False(template!.Active); // nao gera tarefa nos proximos dias por padrao
        Assert.Equal("Fazer desenho", template.Title);
        Assert.Equal("capricho livre", template.Description);
        Assert.Equal(2, template.Points);
    }

    [Fact]
    public async Task AtivarTemplate_FazAtarefaSerGeradaNoDiaSeguinte_SemRedigitarNada()
    {
        var (dailyRoutine, _, templates, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var request = new CreateTaskRequest("Regar as plantas", null, "expected", "morning", 1);
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId, request, userId, "child");
        var templateId = ObjectId.Parse(routine.Tasks[0].TaskTemplateId!);

        // Sem ativar: o dia seguinte NAO deveria trazer a tarefa de volta.
        var nextDayBeforeActivation = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-25", "America/Sao_Paulo");
        Assert.Empty(nextDayBeforeActivation.Tasks);

        // Ativa o template — "replicar em outro dia" sem reescrever titulo/tipo/pontos.
        await templates.ActivateAsync(templateId);

        var followingDay = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-26", "America/Sao_Paulo");
        var replicated = Assert.Single(followingDay.Tasks);
        Assert.Equal("Regar as plantas", replicated.Title);
        Assert.Equal(1, replicated.Points);
    }
}
