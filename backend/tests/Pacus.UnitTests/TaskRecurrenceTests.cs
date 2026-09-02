using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;
using Pacus.Application.Exceptions;

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

        // Type/Period vem do template, iguais em toda variante. Points tambem,
        // quando a variante nao define um valor proprio (ver teste abaixo pro
        // caso em que define).
        Assert.Equal(3, monday.Tasks.Single().Points);
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_VarianteComPontosProprios_SobrescreveOsDoTemplate()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        // Points do template (3) e o padrao -- so a variante de quarta define um
        // valor proprio (5), porque "Chef por um Dia" exige supervisao de adulto.
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
                    new("Monday", "Missão Detetive", null),
                    new("Wednesday", "Chef por um Dia", "Com supervisão de um adulto.", Points: 5),
                }));

        var monday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");
        var wednesday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");

        Assert.Equal(3, monday.Tasks.Single().Points); // sem override -- usa o do template
        Assert.Equal(5, wednesday.Tasks.Single().Points); // override da variante
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_SemVariantes_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Momento Criativo", null, "challenge", "afternoon", 3, Recurrence: "weekday_rotation")));
    }

    [Fact]
    public async Task RecorrenciaWeekdayRotation_VarianteNoFimDeSemana_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
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
    public async Task RecorrenciaCustom_AparecoApenasNosDiasEscolhidos_TercaEQuarta()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        // "Ingles" so terca e quarta -- caso real pedido pelo usuario.
        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Inglês",
                null,
                "challenge",
                "afternoon",
                3,
                Recurrence: "custom",
                CustomDays: new List<string> { "Tuesday", "Wednesday" }));

        var monday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-08-31", "America/Sao_Paulo");
        var tuesday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var wednesday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");
        var thursday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-03", "America/Sao_Paulo");

        Assert.Empty(monday.Tasks);
        Assert.Equal("Inglês", tuesday.Tasks.Single().Title);
        Assert.Equal("Inglês", wednesday.Tasks.Single().Title);
        Assert.Empty(thursday.Tasks);
    }

    [Fact]
    public async Task RecorrenciaCustom_AceitaUmUnicoDiaDeFimDeSemana_Escoteiro()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        // "Escoteiro" so sabado -- diferente de RecurrenceWeekend (sabado E domingo),
        // custom aceita um unico dia, incluindo fim de semana.
        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Escoteiro",
                null,
                "expected",
                "morning",
                2,
                Recurrence: "custom",
                CustomDays: new List<string> { "Saturday" }));

        var friday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-04", "America/Sao_Paulo");
        var saturday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-05", "America/Sao_Paulo");
        var sunday = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-06", "America/Sao_Paulo");

        Assert.Empty(friday.Tasks);
        Assert.Equal("Escoteiro", saturday.Tasks.Single().Title);
        Assert.Empty(sunday.Tasks);
    }

    [Fact]
    public async Task RecorrenciaCustom_SemDias_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Inglês", null, "challenge", "afternoon", 3, Recurrence: "custom")));
    }

    [Fact]
    public async Task RecorrenciaInterval_DiaSimDiaNao_DeslizaPelosDiasDaSemanaAPartirDaAncora()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        // Caso real pedido pelo usuario: "Lavar o cabelo", dia sim dia nao, comecando
        // numa quarta (2026-09-02). Diferente de RecurrenceCustom com dias fixos --
        // aqui o padrao desliza: qua, sex, dom, ter, qui, sab, seg, qua...
        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Lavar o cabelo",
                "Se sentir mais limpo e cheiroso",
                "mandatory",
                "evening",
                1,
                Recurrence: "interval",
                AnchorDate: "2026-09-02",
                IntervalDays: 2));

        var beforeAnchor = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var anchorDay = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo"); // qua (dia 0)
        var dayAfter = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-03", "America/Sao_Paulo"); // qui (dia 1)
        var twoAfter = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-04", "America/Sao_Paulo"); // sex (dia 2)
        var threeAfter = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-05", "America/Sao_Paulo"); // sab (dia 3)
        var fourAfter = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-06", "America/Sao_Paulo"); // dom (dia 4)

        Assert.Empty(beforeAnchor.Tasks); // antes da ancora, nunca aparece
        Assert.Equal("Lavar o cabelo", anchorDay.Tasks.Single().Title);
        Assert.Empty(dayAfter.Tasks);
        Assert.Equal("Lavar o cabelo", twoAfter.Tasks.Single().Title);
        Assert.Empty(threeAfter.Tasks);
        Assert.Equal("Lavar o cabelo", fourAfter.Tasks.Single().Title);
    }

    [Fact]
    public async Task RecorrenciaInterval_SemIntervalDays_UsaPadraoDiaSimDiaNao()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Lavar o cabelo", null, "mandatory", "evening", 1,
                Recurrence: "interval", AnchorDate: "2026-09-02"));

        var anchorDay = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");
        var dayAfter = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-03", "America/Sao_Paulo");

        Assert.Single(anchorDay.Tasks);
        Assert.Empty(dayAfter.Tasks);
    }

    [Fact]
    public async Task RecorrenciaInterval_SemAnchorDate_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Lavar o cabelo", null, "mandatory", "evening", 1, Recurrence: "interval")));
    }

    [Fact]
    public async Task RecorrenciaInterval_AnchorDateInvalida_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Lavar o cabelo", null, "mandatory", "evening", 1,
                Recurrence: "interval", AnchorDate: "02/09/2026")));
    }

    [Fact]
    public async Task RecorrenciaInterval_IntervalDaysMenorQueUm_LancaExcecao()
    {
        var (templates, _) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await Assert.ThrowsAsync<ValidationException>(() => templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Lavar o cabelo", null, "mandatory", "evening", 1,
                Recurrence: "interval", AnchorDate: "2026-09-02", IntervalDays: 0)));
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
