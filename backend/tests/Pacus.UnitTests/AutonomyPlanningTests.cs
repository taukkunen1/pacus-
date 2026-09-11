using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Exceptions;
using Pacus.Application.Services;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md): plano da noite,
// autodeclaracao de iniciativa (com bonus) e de motivo de tarefa nao feita (sem
// nenhuma punicao), e o relatorio semanal de autonomia.
public class AutonomyPlanningTests
{
    private static (DailyRoutineService Service, FakePointTransactionRepository Points) BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templateRepo = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var points = new FakePointTransactionRepository();
        var pointsService = new PointsService(points);
        var service = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        return (service, points);
    }

    [Fact]
    public async Task SetEveningPlanAsync_ComTarefasValidas_GravaOrdemEscolhida()
    {
        var (service, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        var routine = await service.CreateRoutineForDateAsync(userId, "2026-09-10", "America/Sao_Paulo");
        var task = await service.CreateAdHocTaskAsync(
            userId,
            new CreateTaskRequest("Ler", null, "mandatory", "evening", 5),
            actorId,
            "child");
        var taskId = task.Tasks.Single(t => t.Title == "Ler").Id;

        var updated = await service.SetEveningPlanAsync(
            userId,
            new List<EveningPlanItemRequest> { new(taskId, "Depois do banho") },
            actorId,
            "child");

        Assert.Single(updated.EveningPlan);
        Assert.Equal(taskId, updated.EveningPlan[0].TaskId);
        Assert.Equal("Depois do banho", updated.EveningPlan[0].ApproxLabel);
        Assert.NotNull(updated.EveningPlanSetAt);
    }

    [Fact]
    public async Task SetEveningPlanAsync_TarefaInexistente_LancaValidationException()
    {
        var (service, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-10", "America/Sao_Paulo");

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetEveningPlanAsync(
                userId,
                new List<EveningPlanItemRequest> { new("id-que-nao-existe", null) },
                actorId,
                "child"));
    }

    [Fact]
    public async Task SetTaskInitiativeAsync_SelfStarted_ConcedeBonusUmaUnicaVez()
    {
        var (service, points) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        var routine = await service.CreateAdHocTaskAsync(
            userId,
            new CreateTaskRequest("Ler", null, "mandatory", "evening", 5),
            actorId,
            "child");
        var taskId = routine.Tasks.Single().Id;

        await service.SetTaskInitiativeAsync(userId, taskId, TaskInitiativeLevel.SelfStarted, actorId, "child");
        var updated = await service.SetTaskInitiativeAsync(
            userId, taskId, TaskInitiativeLevel.SelfStarted, actorId, "child");

        Assert.Equal(TaskInitiativeLevel.SelfStarted, updated.Tasks.Single().Initiative);
        Assert.Equal(
            DailyRoutineService.InitiativeBonusSelfStarted,
            points.Transactions.Sum(t => t.Points));
    }

    [Fact]
    public async Task SetTaskInitiativeAsync_PromptedByAdult_NaoConcedeBonus()
    {
        var (service, points) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        var routine = await service.CreateAdHocTaskAsync(
            userId,
            new CreateTaskRequest("Ler", null, "mandatory", "evening", 5),
            actorId,
            "child");
        var taskId = routine.Tasks.Single().Id;

        var updated = await service.SetTaskInitiativeAsync(
            userId, taskId, TaskInitiativeLevel.PromptedByAdult, actorId, "adult");

        Assert.Equal(TaskInitiativeLevel.PromptedByAdult, updated.Tasks.Single().Initiative);
        Assert.Empty(points.Transactions);
    }

    [Fact]
    public async Task SetTaskSkipReasonAsync_Outro_GravaNotaLivre_ENaoMexeEmPontos()
    {
        var (service, points) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        var routine = await service.CreateAdHocTaskAsync(
            userId,
            new CreateTaskRequest("Ler", null, "mandatory", "evening", 5),
            actorId,
            "child");
        var taskId = routine.Tasks.Single().Id;

        var updated = await service.SetTaskSkipReasonAsync(
            userId, taskId, TaskSkipReason.Other, "Fui brincar com meu irmão", actorId, "child");

        var task = updated.Tasks.Single();
        Assert.Equal(TaskSkipReason.Other, task.SkipReason);
        Assert.Equal("Fui brincar com meu irmão", task.SkipReasonNote);
        Assert.Equal(Pacus.Domain.Enums.TaskItemStatus.Pending, task.Status);
        Assert.Empty(points.Transactions);
    }

    [Fact]
    public async Task SetTaskSkipReasonAsync_NaoOutro_IgnoraNotaLivre()
    {
        var (service, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        var routine = await service.CreateAdHocTaskAsync(
            userId,
            new CreateTaskRequest("Ler", null, "mandatory", "evening", 5),
            actorId,
            "child");
        var taskId = routine.Tasks.Single().Id;

        var updated = await service.SetTaskSkipReasonAsync(
            userId, taskId, TaskSkipReason.Sleepy, "texto que nao deveria ser salvo", actorId, "child");

        var task = updated.Tasks.Single();
        Assert.Equal(TaskSkipReason.Sleepy, task.SkipReason);
        Assert.Null(task.SkipReasonNote);
    }

    [Fact]
    public async Task GetWeeklyReportAsync_ContaIniciativaPorNivelNaJanelaDeSeteDias()
    {
        var routines = new FakeDailyRoutineRepository();
        var templateRepo = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var points = new FakePointTransactionRepository();
        var pointsService = new PointsService(points);
        var dailyRoutineService = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        var autonomyService = new AutonomyService(routines);

        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        // "Hoje" = 2026-09-10. Uma tarefa concluida sozinha hoje, uma com ajuda do
        // PACUS 3 dias atras (dentro da janela de 7 dias), e uma com lembrete de
        // adulto 10 dias atras (fora da janela -- nao deve contar).
        var today = await dailyRoutineService.CreateRoutineForDateAsync(userId, "2026-09-10", "America/Sao_Paulo");
        var taskToday = (await dailyRoutineService.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Ler", null, "mandatory", "evening", 5), actorId, "child"))
            .Tasks.Single(t => t.Title == "Ler");
        await dailyRoutineService.SetTaskInitiativeAsync(
            userId, taskToday.Id, TaskInitiativeLevel.SelfStarted, actorId, "child");

        var older = await dailyRoutineService.CreateRoutineForDateAsync(userId, "2026-09-07", "America/Sao_Paulo");
        older.Tasks.Add(new Pacus.Domain.Entities.DailyTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Dever de casa",
            Type = Pacus.Domain.Enums.TaskType.Mandatory,
            Period = Pacus.Domain.Enums.TaskPeriod.Afternoon,
            Points = 5,
            Initiative = TaskInitiativeLevel.PromptedByPacus,
            CreatedBy = userId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        // GetHistoryAsync so devolve dias ja fechados -- fecha manualmente pra simular
        // o que o fechamento diario real (IDayClosingService) faria com o tempo.
        older.Status = Pacus.Domain.Enums.RoutineStatus.Closed;
        await routines.UpdateAsync(older);

        var tooOld = await dailyRoutineService.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");
        tooOld.Tasks.Add(new Pacus.Domain.Entities.DailyTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Arrumar a cama",
            Type = Pacus.Domain.Enums.TaskType.Mandatory,
            Period = Pacus.Domain.Enums.TaskPeriod.Morning,
            Points = 5,
            Initiative = TaskInitiativeLevel.PromptedByAdult,
            CreatedBy = userId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        tooOld.Status = Pacus.Domain.Enums.RoutineStatus.Closed;
        await routines.UpdateAsync(tooOld);

        // utcNow fixo (2026-09-10 12:00 em America/Sao_Paulo, UTC-3) faz o teste
        // deterministico -- nao depende da data real em que o CI roda.
        var report = await autonomyService.GetWeeklyReportAsync(
            userId, "America/Sao_Paulo", new DateTime(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, report.SelfStarted);
        Assert.Equal(1, report.PromptedByPacus);
        Assert.Equal(0, report.PromptedByAdult);
    }
}

