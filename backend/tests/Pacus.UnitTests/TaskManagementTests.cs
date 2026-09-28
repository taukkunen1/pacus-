using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;
using Pacus.Application.Exceptions;

namespace Pacus.UnitTests;

// Cenarios criticos #5 (alteracao de ordem) e #8 (tarefa alterada). O antigo
// ajuste manual de pontos pelo adulto (AdjustTaskPointsAsync) foi removido --
// toda tarefa vale exatamente 1 Pacus Point, entao nao ha mais o que ajustar.
public class TaskManagementTests
{
    private static (DailyRoutineService dailyRoutine, FakePointTransactionRepository pointsRepo)
        BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templates = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var pointsRepo = new FakePointTransactionRepository();
        var pointsService = new PointsService(pointsRepo);
        var dailyRoutine = new DailyRoutineService(routines, templates, events, pointsService, new FakeSettingsRepository());
        return (dailyRoutine, pointsRepo);
    }

    [Fact]
    public async Task ReordenarTarefas_AtualizaAOrdemSemAlterarOutrosCampos()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var r1 = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Escovar dentes", null, "mandatory", "morning", 1), userId, "child");
        var r2 = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 1), userId, "child");

        var idFirst = r2.Tasks[0].Id;
        var idSecond = r2.Tasks[1].Id;

        var reordered = await dailyRoutine.ReorderTasksAsync(
            userId, new List<string> { idSecond, idFirst }, userId, "child");

        var lerLivro = reordered.Tasks.First(t => t.Id == idSecond);
        var escovarDentes = reordered.Tasks.First(t => t.Id == idFirst);
        Assert.Equal(1, lerLivro.Order);
        Assert.Equal(2, escovarDentes.Order);
        Assert.Equal(0, lerLivro.Points); // tarefa criada pela crianca nao pode mintar PP
    }

    [Fact]
    public async Task ReordenarComListaIncompleta_LancaExcecao()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");
        await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Escovar dentes", null, "mandatory", "morning", 1), userId, "child");
        await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 1), userId, "child");

        await Assert.ThrowsAsync<ValidationException>(
            () => dailyRoutine.ReorderTasksAsync(userId, new List<string> { "so-um-id" }, userId, "child"));
    }

    [Fact]
    public async Task CriarTarefa_Com1Ponto_NaoLancaExcecao()
    {
        // 2026-09-27: toda tarefa passou a valer exatamente 1 Pacus Point (sem
        // faixa configuravel e sem o antigo endpoint de ajuste manual, removido
        // junto com este commit -- ver docs/ESTADO_ATUAL.md). A cobertura de
        // valores invalidos continua em CriarTarefa_ComZeroPontos_LancaExcecao
        // e CriarTarefa_Com11Pontos_LancaExcecao.
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        var routine = await dailyRoutine.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Tarefa", null, "challenge", "evening", 1), userId, "adult");

        Assert.Equal(1, routine.Tasks.Last().Points);
    }

    [Fact]
    public async Task CriarTarefa_Com11Pontos_LancaExcecao()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        await Assert.ThrowsAsync<ValidationException>(() => dailyRoutine.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Tarefa", null, "challenge", "evening", 11), userId, "child"));
    }

    [Fact]
    public async Task CriarTarefa_ComZeroPontos_LancaExcecao()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        await Assert.ThrowsAsync<ValidationException>(() => dailyRoutine.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Tarefa", null, "challenge", "evening", 0), userId, "child"));
    }

    [Fact]
    public async Task Crianca_PodeEditarEExcluirSomenteATarefaDoDiaAtual()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");
        var created = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 1), userId, "child");
        var taskId = created.Tasks.Last().Id;

        var updated = await dailyRoutine.UpdateTaskAsync(userId, taskId,
            new DailyTaskUpdateRequest("Ler 20 paginas", "Livro escolhido pela crianca", "expected", "evening", 1), userId, "child");
        Assert.Equal("Ler 20 paginas", updated.Tasks.Last().Title);
        Assert.Equal(0, updated.Tasks.Last().Points);

        var deleted = await dailyRoutine.DeleteTaskAsync(userId, taskId, userId, "child");
        Assert.NotNull(deleted.Tasks.Last().DeletedAt);
    }
}