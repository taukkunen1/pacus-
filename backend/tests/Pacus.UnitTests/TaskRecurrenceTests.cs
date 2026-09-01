using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cobre a recorrencia de TaskTemplate (Recurrence/Variants), ate esta mudanca um
// campo gravado no banco mas nunca lido por DailyRoutineService -- toda tarefa
// permanente aparecia em todos os dias, sem excecao. Ver TaskTemplate.Recurrence*
// e DailyRoutineService.ResolveTemplateForDay.
public class TaskRecurrenceTests
{
    private static (TaskTemplateService templates, DailyRoutineService dailyRoutine)
        BuildSystem(out FakeTaskTemplateRepository templateRepo)
    {
        templateRepo = new FakeTaskTemplateRepository();
        var routines = new FakeDailyRoutineRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        var templateService = new TaskTemplateService(templateRepo, new FakeAuditLogRepository());
        var dailyRoutine = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        return (templateService, dailyRoutine);
    }

    [Fact]
    public async Task RecorrenciaWeekday_NaoAparecoNoFimDeSemana()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Duolingo", null, "challenge", "morning", 3, Recurrence: "weekday"));

        // 2026-08-29 = sabado, 2026-08-31 = segunda (confirmar com DateTime.DayOfWeek).
        var saturday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-29", "America/Sao_Paulo");
        var monday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");

        Assert.Empty(saturday.Tasks);
        Assert.Single(monday.Tasks);
        Assert.Equal("Duolingo", monday.Tasks[0].Title);
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_TrocaConteudoPorDiaDaSemanaEExcluiFimDeSemana()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Momento Criativo",
                null,
                "challenge",
                "afternoon",
                3,
                Recurrence: "weekday_rotation",
                Variants: new List<TaskVariantRequest>
                {
                    new("Monday", "Missão Detetive", "Encontrar 5 coisas fora do lugar em casa."),
                    new("Tuesday", "Desafio Engenheiro", "Construir uma torre ou ponte que fique de pé por 30s."),
                    new("Wednesday", "Chef por um Dia", "Preparar um lanche simples com supervisão."),
                    new("Thursday", "Inventor Maluco", "Criar um objeto com sucata que resolva um problema."),
                    new("Friday", "Missão 20 Minutos", "Deixar uma área da casa melhor em 20 minutos."),
                }));

        // 2026-08-31 = segunda, 09-01 = terca, ..., 09-04 = sexta, 09-05 = sabado.
        var monday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");
        var tuesday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var friday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-04", "America/Sao_Paulo");
        var saturday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-05", "America/Sao_Paulo");

        Assert.Equal("Missão Detetive", monday.Tasks.Single().Title);
        Assert.Equal("Desafio Engenheiro", tuesday.Tasks.Single().Title);
        Assert.Equal("Missão 20 Minutos", friday.Tasks.Single().Title);
        Assert.Empty(saturday.Tasks); // sem variante pra sabado -- nao aparece

        // Type/Period/Points vem do template, iguais em toda variante.
        Assert.Equal(3, monday.Tasks.Single().Points);
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_SemVariantes_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<InvalidOperationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Momento Criativo", null, "challenge", "afternoon", 3, Recurrence: "weekday_rotation")));
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_VarianteNoFimDeSemana_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<InvalidOperationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Momento Criativo",
                null,
                "challenge",
                "afternoon",
                3,
                Recurrence: "weekday_rotation",
                Variants: new List<TaskVariantRequest> { new("Saturday", "Passeio", null) })));
    }

    [Fact]
    public async Task RecorrenciaDaily_ContinuaAparecendoTodoDia_ComportamentoOriginal()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Escovar dentes", null, "mandatory", "morning", 1));

        var saturday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-29", "America/Sao_Paulo");
        var monday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");

        Assert.Single(saturday.Tasks);
        Assert.Single(monday.Tasks);
    }
}
