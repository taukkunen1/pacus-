using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cenarios criticos #5 (alteracao de ordem) e #8 (tarefa alterada), mais o
// caso de ajuste de pontos pelo adulto apos negociacao com a crianca.
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
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 3), userId, "child");

        var idFirst = r2.Tasks[0].Id;
        var idSecond = r2.Tasks[1].Id;

        var reordered = await dailyRoutine.ReorderTasksAsync(
            userId, new List<string> { idSecond, idFirst }, userId, "child");

        var lerLivro = reordered.Tasks.First(t => t.Id == idSecond);
        var escovarDentes = reordered.Tasks.First(t => t.Id == idFirst);
        Assert.Equal(1, lerLivro.Order);
        Assert.Equal(2, escovarDentes.Order);
        Assert.Equal(3, lerLivro.Points); // pontos nao mudam so por reordenar
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
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 3), userId, "child");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dailyRoutine.ReorderTasksAsync(userId, new List<string> { "so-um-id" }, userId, "child"));
    }

    [Fact]
    public async Task AjustarPontos_TarefaAindaPendente_NaoGeraTransacao()
    {
        var (dailyRoutine, pointsRepo) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Arrumar quarto", null, "expected", "afternoon", 3), userId, "child");
        var taskId = routine.Tasks[0].Id;

        var updated = await dailyRoutine.AdjustTaskPointsAsync(userId, taskId, 2, userId, "adult");

        Assert.Equal(2, updated.Tasks[0].Points);
        Assert.Empty(pointsRepo.Transactions); // ainda nao foi concluida, nada de dinheiro se move
    }

    [Fact]
    public async Task AjustarPontos_TarefaJaConcluida_GeraTransacaoDeAjusteComODelta()
    {
        var (dailyRoutine, pointsRepo) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");
        var routine = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Arrumar quarto", null, "expected", "afternoon", 3), userId, "child");
        var taskId = routine.Tasks[0].Id;

        await dailyRoutine.ToggleTaskAsync(userId, taskId, true, userId, "child"); // crianca sugeriu 3, ganhou 3
        Assert.Equal(3, await pointsRepo.GetBalanceAsync(userId));

        await dailyRoutine.AdjustTaskPointsAsync(userId, taskId, 2, userId, "adult"); // adulto aprova so 2

        Assert.Equal(2, await pointsRepo.GetBalanceAsync(userId)); // saldo reflete o valor final
        Assert.Equal(2, pointsRepo.Transactions.Count(t => t.UserId == userId)); // award + adjustment
    }

    [Fact]
    public async Task CriarTarefa_Com4Pontos_LancaExcecao()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        await Assert.ThrowsAsync<InvalidOperationException>(() => dailyRoutine.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Tarefa", null, "challenge", "evening", 4), userId, "child"));
    }

    [Fact]
    public async Task CriarTarefa_ComZeroPontos_LancaExcecao()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");

        await Assert.ThrowsAsync<InvalidOperationException>(() => dailyRoutine.CreateAdHocTaskAsync(
            userId, new CreateTaskRequest("Tarefa", null, "challenge", "evening", 0), userId, "child"));
    }

    [Fact]
    public async Task Crianca_PodeEditarEExcluirSomenteATarefaDoDiaAtual()
    {
        var (dailyRoutine, _) = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-24", "America/Sao_Paulo");
        var created = await dailyRoutine.CreateAdHocTaskAsync(userId,
            new CreateTaskRequest("Ler livro", null, "expected", "evening", 2), userId, "child");
        var taskId = created.Tasks.Last().Id;

        var updated = await dailyRoutine.UpdateTaskAsync(userId, taskId,
            new DailyTaskUpdateRequest("Ler 20 paginas", "Livro escolhido pela crianca", "expected", "evening", 3), userId, "child");
        Assert.Equal("Ler 20 paginas", updated.Tasks.Last().Title);
        Assert.Equal(3, updated.Tasks.Last().Points);

        var deleted = await dailyRoutine.DeleteTaskAsync(userId, taskId, userId, "child");
        Assert.NotNull(deleted.Tasks.Last().DeletedAt);
    }
}