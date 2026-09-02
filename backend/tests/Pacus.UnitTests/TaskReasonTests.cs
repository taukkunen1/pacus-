using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;
using Pacus.Application.Exceptions;

namespace Pacus.UnitTests;

// Cobre TaskTemplate.Reasons (pool de motivos, 2026-09-02: "frases aleatorias e
// motivos sempre pertinentes, nao precisa ser a mesma frase todos os dias" --
// pedido do dono do produto) e o sorteio feito por DailyRoutineService a cada
// DailyTask gerado. Ver TaskTemplate.EffectiveReasons/Reasons e
// DailyRoutineService.PickReason.
public class TaskReasonTests
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
    public void ParseReasons_SemNadaInformado_RetornaListaVazia()
    {
        Assert.Empty(TaskTemplateService.ParseReasons(null, null));
        Assert.Empty(TaskTemplateService.ParseReasons(new List<string>(), ""));
    }

    [Fact]
    public void ParseReasons_TrimaEDescartaVaziosEDuplicados()
    {
        var reasons = TaskTemplateService.ParseReasons(
            new List<string> { "  Beber água ajuda a acordar.  ", "", "   ", "beber água ajuda a acordar." },
            null);

        // "beber água ajuda a acordar." (minusculo) e duplicata case-insensitive
        // da primeira frase -- so a primeira grafia e mantida.
        Assert.Equal(new List<string> { "Beber água ajuda a acordar." }, reasons);
    }

    [Fact]
    public void ParseReasons_CaiParaLegacyReason_QuandoReasonsNaoVemPreenchido()
    {
        var reasons = TaskTemplateService.ParseReasons(null, "  Motivo antigo.  ");
        Assert.Equal(new List<string> { "Motivo antigo." }, reasons);
    }

    [Fact]
    public void ParseReasons_PriorizaReasonsSobreLegacyReason_QuandoOsDoisVem()
    {
        var reasons = TaskTemplateService.ParseReasons(
            new List<string> { "Motivo novo." }, "Motivo antigo ignorado.");

        Assert.Equal(new List<string> { "Motivo novo." }, reasons);
    }

    [Fact]
    public void ParseReasons_MaisDeOito_LancaValidationException()
    {
        var nove = Enumerable.Range(1, 9).Select(i => $"Motivo {i}").ToList();
        Assert.Throws<ValidationException>(() => TaskTemplateService.ParseReasons(nove, null));
    }

    [Fact]
    public async Task CreateAsync_ComListaDeMotivos_GravaReasonsENaoOLegado()
    {
        var (templates, _) = BuildSystem(out var templateRepo);
        var userId = ObjectId.GenerateNewId();

        var created = await templates.CreateAsync(userId, userId,
            new CreateTaskRequest(
                "Beber água", null, "mandatory", "morning", 1,
                Reasons: new List<string>
                {
                    "Seu corpo passa a noite sem beber nada — um copo de água de manhã ajuda a acordar.",
                    "Beber água logo cedo ajuda a pensar com mais clareza o resto do dia.",
                }));

        Assert.Equal(2, created.Reasons.Count);
        Assert.Null(created.Reason);
    }

    [Fact]
    public async Task TarefaComVariosMotivos_DailyTaskRecebeUmDelesDoPool()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        var reasons = new List<string> { "Motivo A", "Motivo B", "Motivo C" };
        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Arrumar a cama", null, "mandatory", "morning", 1, Reasons: reasons));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");

        var task = Assert.Single(routine.Tasks);
        Assert.Contains(task.Reason, reasons);
    }

    [Fact]
    public async Task TarefaComVariosMotivos_SorteioVariaEntreDiasDiferentes()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        var reasons = new List<string> { "Motivo A", "Motivo B" };
        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Escovar os dentes", null, "mandatory", "morning", 1, Reasons: reasons));

        // Gera a rotina de 40 dias diferentes (probabilidade de sortear sempre o
        // mesmo dos 2 motivos nas 40 vezes: 0.5^39, estatisticamente impossivel de
        // acontecer por acaso -- se isso falhar, o sorteio parou de sortear de
        // verdade, nao e flakiness). Datas fake que nunca caem no mesmo dia da
        // semana com significado especial (essa tarefa e "mandatory"/recorrencia
        // padrao "daily", entao aparece em todo dia).
        var reasonsSeen = new HashSet<string>();
        for (var day = 1; day <= 40; day++)
        {
            var date = new DateTime(2026, 1, 1).AddDays(day).ToString("yyyy-MM-dd");
            var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, date, "America/Sao_Paulo");
            reasonsSeen.Add(Assert.Single(routine.Tasks).Reason!);
        }

        Assert.Equal(2, reasonsSeen.Count);
    }

    [Fact]
    public async Task TemplateAntigoSoComReasonLegado_EffectiveReasonsCaiParaEle()
    {
        var (_, dailyRoutine) = BuildSystem(out var templateRepo);
        var userId = ObjectId.GenerateNewId();

        // Simula um documento do Mongo gravado antes desta mudanca: so o campo
        // Reason (singular) preenchido, Reasons nunca existiu (lista vazia default).
        // Inserido direto no repositorio (nao via TaskTemplateService, que sempre
        // zera Reason ao salvar) -- e exatamente esse o caso que EffectiveReasons
        // existe para cobrir.
        await templateRepo.CreateAsync(new TaskTemplate
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = userId,
            Title = "Tarefa legada",
            Type = TaskType.Mandatory,
            Period = TaskPeriod.Morning,
            Points = 1,
            Active = true,
            Recurrence = TaskTemplate.RecurrenceDaily,
            Reason = "Motivo escrito antes desta mudanca.",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");

        var task = Assert.Single(routine.Tasks);
        Assert.Equal("Motivo escrito antes desta mudanca.", task.Reason);
    }

    [Fact]
    public async Task TarefaSemMotivoNenhum_DailyTaskFicaComReasonNulo()
    {
        var (templates, dailyRoutine) = BuildSystem(out _);
        var userId = ObjectId.GenerateNewId();

        await templates.CreateAsync(userId, userId,
            new CreateTaskRequest("Tarefa sem motivo", null, "mandatory", "morning", 1));

        var routine = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-02", "America/Sao_Paulo");

        Assert.Null(Assert.Single(routine.Tasks).Reason);
    }
}
