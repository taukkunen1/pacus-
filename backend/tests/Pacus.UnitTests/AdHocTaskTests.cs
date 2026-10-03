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
    [Fact]
    public async Task PermanentDeletion_RemovesAlreadyPlannedOccurrence()
    {
        var (service, routines, _, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-08-24", "America/Sao_Paulo");
        var today = await service.CreateAdHocTaskAsync(familyId,
            new CreateTaskRequest("Ler", null, "expected", "evening", 3, Permanent: true), familyId, "adult");
        var planned = await service.CreateRoutineForDateAsync(familyId, "2026-08-25", "America/Sao_Paulo");
        planned.Status = Pacus.Domain.Enums.RoutineStatus.Planned;
        planned.TomorrowPlanConfirmedAt = DateTime.UtcNow;
        await routines.UpdateAsync(planned);
        await service.DeleteTaskAsync(familyId, today.Tasks[0].Id, familyId, "child", permanent: true);
        var saved = await routines.GetByUserAndDateAsync(familyId, "2026-08-25");
        Assert.NotNull(saved!.Tasks[0].DeletedAt);
        Assert.Null(saved.TomorrowPlanConfirmedAt);
    }

    [Fact]
    public async Task PermanentActions_RespectDisabledChildPermissions()
    {
        var routines = new FakeDailyRoutineRepository();
        var templates = new FakeTaskTemplateRepository();
        var settings = new FakeSettingsRepository(new Settings
        {
            ChildPermissions = new ChildPermissions { CanCreateTasks = false, CanDeleteTasks = false }
        });
        var service = new DailyRoutineService(routines, templates, new FakeTaskEventRepository(),
            new PointsService(new FakePointTransactionRepository()), settings);
        var familyId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-08-24", "America/Sao_Paulo");
        var request = new CreateTaskRequest("Ler", null, "expected", "evening", 3, Permanent: true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAdHocTaskAsync(familyId, request, familyId, "child"));
        Assert.Empty(await templates.GetActiveByUserAsync(familyId));
        var today = await service.CreateAdHocTaskAsync(familyId, request, familyId, "adult");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteTaskAsync(familyId, today.Tasks[0].Id, familyId, "child", permanent: true));
        Assert.Single(await templates.GetActiveByUserAsync(familyId));
    }

    [Theory]
    [InlineData("adult", 3)]
    [InlineData("child", 0)]
    public async Task PermanentTask_RepeatsWithDescriptionAndSafePoints(string role, int points)
    {
        var (service, _, templates, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-08-24", "America/Sao_Paulo");
        var today = await service.CreateAdHocTaskAsync(familyId,
            new CreateTaskRequest("Ler", "Ler dez minutos", "expected", "evening", 3, Permanent: true),
            familyId, role);
        var template = await templates.GetByIdAsync(ObjectId.Parse(today.Tasks[0].TaskTemplateId!));
        Assert.True(template!.Active);
        var tomorrow = await service.CreateRoutineForDateAsync(familyId, "2026-08-25", "America/Sao_Paulo");
        var task = Assert.Single(tomorrow.Tasks);
        Assert.Equal("Ler dez minutos", task.Description);
        Assert.Equal(points, task.Points);
    }

    [Theory]
    [InlineData("adult", false)]
    [InlineData("adult", true)]
    [InlineData("child", false)]
    [InlineData("child", true)]
    public async Task DeletePermanentTask_RespectsScope(string role, bool permanent)
    {
        var (service, _, templates, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-08-24", "America/Sao_Paulo");
        var today = await service.CreateAdHocTaskAsync(familyId,
            new CreateTaskRequest("Ler", null, "expected", "evening", 3, Permanent: true), familyId, role);
        var removed = await service.DeleteTaskAsync(familyId, today.Tasks[0].Id, familyId, role, permanent);
        Assert.NotNull(removed.Tasks[0].DeletedAt);
        var template = await templates.GetByIdAsync(ObjectId.Parse(today.Tasks[0].TaskTemplateId!));
        Assert.Equal(!permanent, template!.Active);
        var tomorrow = await service.CreateRoutineForDateAsync(familyId, "2026-08-25", "America/Sao_Paulo");
        Assert.Equal(permanent ? 0 : 1, tomorrow.Tasks.Count);
    }

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
    public async Task CriancaCriaTarefaNova_EntraNaRotinaSemPoderCriarPontos()
    {
        var (dailyRoutine, routines, _, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var request = new CreateTaskRequest("Comprar racao", null, "challenge", "afternoon", 3);
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId, request, userId, "child");

        var created = Assert.Single(routine.Tasks);
        Assert.Equal("Comprar racao", created.Title);
        Assert.Equal(0, created.Points);
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
        Assert.Equal(0, template.Points);
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

        // Ativa o template — a tarefa criada pela crianca continua sem recompensa ate o adulto definir pontos.
        await templates.ActivateAsync(templateId);

        var followingDay = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-26", "America/Sao_Paulo");
        var replicated = Assert.Single(followingDay.Tasks);
        Assert.Equal("Regar as plantas", replicated.Title);
        Assert.Equal(0, replicated.Points);
    }
}
