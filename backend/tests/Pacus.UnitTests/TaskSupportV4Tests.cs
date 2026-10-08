using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Exceptions;
using Pacus.Application.Services;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

public class TaskSupportV4Tests
{
    private static (TaskTemplateService Templates, DailyRoutineService Routines, FakePointTransactionRepository Points)
        BuildSystem()
    {
        var templates = new FakeTaskTemplateRepository();
        var routines = new FakeDailyRoutineRepository();
        var points = new FakePointTransactionRepository();
        return (
            new TaskTemplateService(templates, new FakeAuditLogRepository()),
            new DailyRoutineService(routines, templates, new FakeTaskEventRepository(),
                new PointsService(points), new FakeSettingsRepository()),
            points);
    }

    [Theory]
    [InlineData("reading", 3)]
    [InlineData("homework", 4)]
    [InlineData("handwriting", 3)]
    public async Task Template_HabilitadoPeloAdulto_GeraEtapasEmCopiaDiaria(string kind, int count)
    {
        var (templates, routines, _) = BuildSystem();
        var family = ObjectId.GenerateNewId();
        await templates.CreateAsync(family, family,
            new CreateTaskRequest("Missao", null, "mandatory", "afternoon", 1, SupportKind: kind));

        var day = await routines.CreateRoutineForDateAsync(family, "2026-10-08", "America/Sao_Paulo");
        Assert.Equal(kind, Assert.Single(day.Tasks).SupportKind);
        Assert.Equal(count, day.Tasks.Single().SupportSteps.Count);
    }

    [Theory]
    [InlineData("2026-10-07", false)]
    [InlineData("2026-10-08", true)]
    [InlineData("2026-10-09", false)]
    [InlineData("2026-10-10", true)]
    [InlineData("2026-10-11", false)]
    public void AlternanciaCabelo_UsaDataDaRotina(string date, bool expected)
    {
        Assert.Equal(expected, TaskSupportConfiguration.IsHairWashDay(date));
    }

    [Fact]
    public async Task Banho_Diario_MantemUmaTarefaEPontosSemRecompensaPorEtapa()
    {
        var (templates, routines, points) = BuildSystem();
        var family = ObjectId.GenerateNewId();
        await templates.CreateAsync(family, family,
            new CreateTaskRequest("Tomar banho", null, "mandatory", "evening", 1,
                SupportKind: "bathing"));

        var yesterday = await routines.CreateRoutineForDateAsync(
            family, "2026-10-07", "America/Sao_Paulo");
        var today = await routines.CreateRoutineForDateAsync(
            family, "2026-10-08", "America/Sao_Paulo");
        var tomorrow = await routines.CreateRoutineForDateAsync(
            family, "2026-10-09", "America/Sao_Paulo");

        Assert.Single(yesterday.Tasks);
        Assert.Single(today.Tasks);
        Assert.Single(tomorrow.Tasks);
        Assert.DoesNotContain("Lavar o cabelo", yesterday.Tasks.Single().SupportSteps);
        Assert.Contains("Lavar o cabelo", today.Tasks.Single().SupportSteps);
        Assert.DoesNotContain("Lavar o cabelo", tomorrow.Tasks.Single().SupportSteps);
        Assert.Equal(5, yesterday.Tasks.Single().SupportSteps.Count);
        Assert.Equal(6, today.Tasks.Single().SupportSteps.Count);
        Assert.Equal(5, tomorrow.Tasks.Single().SupportSteps.Count);
        Assert.Equal(1, today.Tasks.Single().Points);

        // RecordSupportActionAsync opera sobre a ultima rotina aberta. Os
        // snapshots historicos acima sao testados separadamente; aqui criamos
        // a rotina de hoje como a mais recente para exercitar as acoes.
        var (actionTemplates, actionRoutines, actionPoints) = BuildSystem();
        var actionFamily = ObjectId.GenerateNewId();
        await actionTemplates.CreateAsync(actionFamily, actionFamily,
            new CreateTaskRequest("Tomar banho", null, "mandatory", "evening", 1,
                SupportKind: "bathing"));
        var active = await actionRoutines.CreateRoutineForDateAsync(
            actionFamily, "2026-10-08", "America/Sao_Paulo");
        var taskId = active.Tasks.Single().Id;
        await actionRoutines.RecordSupportActionAsync(actionFamily, taskId,
            new("step", 0), actionFamily, "child");
        await actionRoutines.RecordSupportActionAsync(actionFamily, taskId,
            new("step", 2), actionFamily, "child");
        Assert.Empty(actionPoints.Transactions);
        Assert.Equal(2, active.Tasks.Single().CompletedSupportSteps.Count);
    }

    [Fact]
    public void Banho_NaoPermiteRemoverRegraDeAlternanciaComEtapasPersonalizadas()
    {
        Assert.Throws<ValidationException>(() =>
            TaskSupportConfiguration.Parse("bathing",
                new List<string> { "Pegar toalha", "Tomar banho" }));
    }

    [Fact]
    public void Configuracao_Invalida_NaoEPermitida()
    {
        Assert.Throws<ValidationException>(() => TaskSupportConfiguration.Parse("outro", null));
        Assert.Throws<ValidationException>(() => TaskSupportConfiguration.Parse(null, new List<string> { "A", "B" }));
        Assert.Throws<ValidationException>(() =>
            TaskSupportConfiguration.Parse("reading", new List<string> { "Uma etapa" }));
    }

    [Fact]
    public async Task EtapasEAjudaEAdiamento_NaoGeramPontos_EPersistem()
    {
        var (templates, routines, points) = BuildSystem();
        var family = ObjectId.GenerateNewId();
        await templates.CreateAsync(family, family, new CreateTaskRequest(
            "Ler o livro", null, "mandatory", "evening", 1, SupportKind: "reading"));
        var day = await routines.CreateRoutineForDateAsync(family, "2026-10-08", "America/Sao_Paulo");
        var taskId = Assert.Single(day.Tasks).Id;

        await routines.RecordSupportActionAsync(family, taskId, new("start"), family, "child");
        await routines.RecordSupportActionAsync(family, taskId, new("step", 0), family, "child");
        await routines.RecordSupportActionAsync(family, taskId, new("step", 0), family, "child");
        await routines.RecordSupportActionAsync(family, taskId, new("postpone"), family, "child");
        await routines.RecordSupportActionAsync(family, taskId, new("help"), family, "child");
        var updated = await routines.RecordSupportActionAsync(family, taskId, new("resume"), family, "child");

        Assert.Equal(new List<int> { 0 }, updated.Tasks.Single().CompletedSupportSteps);
        Assert.Equal(1, updated.Tasks.Single().SupportPostponeCount);
        Assert.Equal(1, updated.Tasks.Single().SupportHelpCount);
        Assert.Null(updated.Tasks.Single().SupportPostponedUntil);
        Assert.NotNull(updated.Tasks.Single().SupportStartedAt);
        Assert.Empty(points.Transactions);

        await routines.SetTaskInitiativeAsync(family, taskId, TaskInitiativeLevel.PromptedByAdult, family, "adult");
        await routines.SetTaskInitiativeAsync(family, taskId, TaskInitiativeLevel.SelfStarted, family, "child");
        Assert.Equal(1, points.Transactions.Sum(p => p.Points));

        await routines.ToggleTaskAsync(family, taskId, true, family, "child");
        Assert.Equal(2, points.Transactions.Sum(p => p.Points));
    }

    [Fact]
    public async Task Acao_EmTarefaNaoHabilitada_OuIndiceInvalido_Rejeitada()
    {
        var (templates, routines, _) = BuildSystem();
        var family = ObjectId.GenerateNewId();
        await templates.CreateAsync(family, family,
            new CreateTaskRequest("Comum", null, "mandatory", "morning", 1));
        await templates.CreateAsync(family, family,
            new CreateTaskRequest("Caligrafia", null, "mandatory", "evening", 1, SupportKind: "handwriting"));

        var day = await routines.CreateRoutineForDateAsync(family, "2026-10-08", "America/Sao_Paulo");
        var regular = day.Tasks.Single(t => t.Title == "Comum");
        var study = day.Tasks.Single(t => t.Title == "Caligrafia");
        await Assert.ThrowsAsync<ValidationException>(() =>
            routines.RecordSupportActionAsync(family, regular.Id, new("start"), family, "child"));
        await Assert.ThrowsAsync<ValidationException>(() =>
            routines.RecordSupportActionAsync(family, study.Id, new("step", 99), family, "child"));
    }
}
