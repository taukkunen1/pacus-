using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;
using Pacus.Application.Exceptions;

namespace Pacus.UnitTests;

// Cobre o campo Options (escolha real pra crianca -- Teoria da Autodeterminacao,
// ver docs/PROPOSITO.md): TaskTemplateService.ParseOptions (validacao) e o fluxo
// completo de tarefa permanente -> DailyTask -> SelectTaskOptionAsync.
public class TaskOptionsTests
{
    private static (TaskTemplateService templates, DailyRoutineService dailyRoutine)
        BuildSystem()
    {
        var templateRepo = new FakeTaskTemplateRepository();
        var routines = new FakeDailyRoutineRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        var templateService = new TaskTemplateService(templateRepo, new FakeAuditLogRepository());
        var dailyRoutine = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        return (templateService, dailyRoutine);
    }

    [Fact]
    public void ParseOptions_SemOpcoes_RetornaListaVazia()
    {
        Assert.Empty(TaskTemplateService.ParseOptions(null));
        Assert.Empty(TaskTemplateService.ParseOptions(new List<string>()));
    }

    [Fact]
    public void ParseOptions_UmaUnicaOpcao_LancaExcecao()
    {
        Assert.Throws<ValidationException>(() =>
            TaskTemplateService.ParseOptions(new List<string> { "Só uma" }));
    }

    [Fact]
    public void ParseOptions_MaisDeQuatroOpcoes_LancaExcecao()
    {
        Assert.Throws<ValidationException>(() =>
            TaskTemplateService.ParseOptions(new List<string> { "A", "B", "C", "D", "E" }));
    }

    [Fact]
    public void ParseOptions_OpcaoEmBranco_LancaExcecao()
    {
        Assert.Throws<ValidationException>(() =>
            TaskTemplateService.ParseOptions(new List<string> { "A", "  " }));
    }

    [Fact]
    public void ParseOptions_OpcoesDuplicadas_LancaExcecao()
    {
        Assert.Throws<ValidationException>(() =>
            TaskTemplateService.ParseOptions(new List<string> { "Torre", "torre" }));
    }

    [Fact]
    public void ParseOptions_DuasATresOpcoesValidas_RetornaTrimadas()
    {
        var result = TaskTemplateService.ParseOptions(new List<string> { " Torre ", "Ponte", "Abrigo" });
        Assert.Equal(new List<string> { "Torre", "Ponte", "Abrigo" }, result);
    }

    [Fact]
    public async Task TarefaPermanenteComOpcoes_CopiaOpcoesParaDailyTask()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Desafio Engenheiro", null, "challenge", "afternoon", 4,
                Options: new List<string> { "Torre de copos", "Ponte de papel", "Abrigo" }));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        var task = Assert.Single(routine.Tasks);
        Assert.Equal(3, task.Options.Count);
        Assert.Null(task.SelectedOption);
    }

    [Fact]
    public async Task SelectTaskOptionAsync_OpcaoValida_GravaEscolha()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Desafio Engenheiro", null, "challenge", "afternoon", 4,
                Options: new List<string> { "Torre de copos", "Ponte de papel" }));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var taskId = routine.Tasks.Single().Id;

        var updated = await dailyRoutine.SelectTaskOptionAsync(userId, taskId, "Ponte de papel", userId, "child");

        Assert.Equal("Ponte de papel", updated.Tasks.Single().SelectedOption);
    }

    [Fact]
    public async Task SelectTaskOptionAsync_OpcaoQueNaoExiste_LancaExcecao()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Desafio Engenheiro", null, "challenge", "afternoon", 4,
                Options: new List<string> { "Torre de copos", "Ponte de papel" }));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var taskId = routine.Tasks.Single().Id;

        await Assert.ThrowsAsync<ValidationException>(() =>
            dailyRoutine.SelectTaskOptionAsync(userId, taskId, "Opção inexistente", userId, "child"));
    }

    [Fact]
    public async Task SelectTaskOptionAsync_Null_LimpaEscolha()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Desafio Engenheiro", null, "challenge", "afternoon", 4,
                Options: new List<string> { "Torre de copos", "Ponte de papel" }));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var taskId = routine.Tasks.Single().Id;

        await dailyRoutine.SelectTaskOptionAsync(userId, taskId, "Torre de copos", userId, "child");
        var updated = await dailyRoutine.SelectTaskOptionAsync(userId, taskId, null, userId, "child");

        Assert.Null(updated.Tasks.Single().SelectedOption);
    }

    // Cobre o campo Reason ("por que isso importa" -- parentalidade
    // autonomo-suportiva, ver docs/PROPOSITO.md e TaskTemplate.Reason/Reasons).
    // ParseSingleReason e o parser de UM motivo (usado ao editar um DailyTask
    // especifico); ParseReasons (pool, ver TaskReasonTests.cs) e o parser novo
    // usado por TaskTemplateService.Create/UpdateAsync.
    [Fact]
    public void ParseSingleReason_NuloOuVazio_RetornaNull()
    {
        Assert.Null(TaskTemplateService.ParseSingleReason(null));
        Assert.Null(TaskTemplateService.ParseSingleReason(""));
        Assert.Null(TaskTemplateService.ParseSingleReason("   "));
    }

    [Fact]
    public void ParseSingleReason_ComTexto_RetornaTrimado()
    {
        Assert.Equal(
            "Aprender coisas novas te ajuda a crescer.",
            TaskTemplateService.ParseSingleReason("  Aprender coisas novas te ajuda a crescer.  "));
    }

    [Fact]
    public async Task TarefaPermanenteComReason_CopiaReasonParaDailyTask()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Escola", null, "mandatory", "morning", 2,
                Reason: "Aprender coisas novas te ajuda a crescer e ter mais escolhas."));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        var task = Assert.Single(routine.Tasks);
        Assert.Equal("Aprender coisas novas te ajuda a crescer e ter mais escolhas.", task.Reason);
    }

    [Fact]
    public async Task UpdateTaskAsync_ReasonEmBranco_LimpaMotivo()
    {
        var (templates, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Escola", null, "mandatory", "morning", 2,
                Reason: "Motivo original"));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var task = routine.Tasks.Single();

        var updated = await dailyRoutine.UpdateTaskAsync(
            userId, task.Id,
            new DailyTaskUpdateRequest(task.Title, task.Description, "mandatory", "morning", task.Points, Reason: "   "),
            userId, "adult");

        Assert.Null(updated.Tasks.Single().Reason);
    }
}
