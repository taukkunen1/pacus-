using MongoDB.Bson;
using Pacus.Application.Services;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Clock fixo: os testes de fechamento de dia nunca devem depender da data real de execucao.
file class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}

// Cenarios criticos da especificacao:
// 1. novo dia
// 2. dia sem tarefas concluidas
// 3. dia completo
// 4. crescimento duplicado
// 5. alteracao de ordem*
// 6. tarefa adicionada*
// 7. tarefa removida*
// 8. tarefa alterada*
// 9. crianca tentando alterar historico*
// 10. crianca tentando alterar configuracao*
// 11. reversao de tarefa
// 12. pontos duplicados
// (* fora do escopo do fechamento do dia em si: pertencem a outros services/camada de autorizacao)
public class DayClosingServiceTests
{
    private static (
        DayClosingService dayClosing,
        DailyRoutineService dailyRoutine,
        FakeDailyRoutineRepository routines,
        FakePacusRepository pacusRepo,
        FakePacusGrowthRepository growthRepo,
        FakePointTransactionRepository pointsRepo)
        BuildSystem(
            List<TaskTemplate>? templates = null,
            DateTime? simulatedUtcNow = null)
    {
        var routines =
            new FakeDailyRoutineRepository();

        var taskTemplates =
            new FakeTaskTemplateRepository(
                templates ?? new());

        var events =
            new FakeTaskEventRepository();

        var pointsRepo =
            new FakePointTransactionRepository();

        var pointsService =
            new PointsService(pointsRepo);

        var dailyRoutineService =
            new DailyRoutineService(
                routines,
                taskTemplates,
                events,
                pointsService,
                new FakeSettingsRepository());

        var pacusRepo =
            new FakePacusRepository();

        var growthRepo =
            new FakePacusGrowthRepository();

        var settingsRepo =
            new FakeSettingsRepository();

        var clock =
            new FixedClock(
                simulatedUtcNow ??
                new DateTime(
                    2026,
                    8,
                    24,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc));

        var dayClosingService =
            new DayClosingService(
                routines,
                dailyRoutineService,
                pacusRepo,
                growthRepo,
                settingsRepo,
                clock);

        return (
            dayClosingService,
            dailyRoutineService,
            routines,
            pacusRepo,
            growthRepo,
            pointsRepo);
    }

    private static Pacus.Domain.Entities.Pacus NewPacus(
        ObjectId familyId) =>
        new()
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Name = "Pacus",
            Species = "axolotl",
            BirthDate = DateTime.UtcNow,
            Stage = PacusStage.Egg,
            TotalClosedDays = 0,
            LastGrowthDate = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task PrimeiroAcesso_SemRotinaAberta_CriaRotinaDeHoje()
    {
        var (
            dayClosing,
            _,
            routines,
            _,
            _,
            _) = BuildSystem();

        var userId =
            ObjectId.GenerateNewId();

        await dayClosing.CloseIfDueAsync(
            userId,
            "America/Sao_Paulo");

        var open =
            await routines.GetLatestOpenAsync(
                userId);

        Assert.NotNull(open);

        Assert.Equal(
            RoutineStatus.Open,
            open!.Status);
    }

    [Fact]
    public async Task DiaSemTarefasConcluidas_FechaEContinuaCrescendoPacus()
    {
        var (
            dayClosing,
            dailyRoutine,
            routines,
            pacusRepo,
            growthRepo,
            _) = BuildSystem();

        var userId =
            ObjectId.GenerateNewId();

        await pacusRepo.CreateAsync(
            NewPacus(userId));

        await dailyRoutine.CreateRoutineForDateAsync(
            userId,
            "2026-08-23",
            "America/Sao_Paulo");

        // Nenhuma tarefa concluida: 0/N.

        // Fecha o dia 23 simulando que "hoje" ja e 24.
        await CloseUpTo(
            dayClosing,
            userId,
            "2026-08-24");

        var closed =
            await routines.GetByUserAndDateAsync(
                userId,
                "2026-08-23");

        Assert.Equal(
            RoutineStatus.Closed,
            closed!.Status);

        var pacus =
            await pacusRepo.GetByFamilyIdAsync(
                userId);

        Assert.Equal(
            1,
            pacus!.TotalClosedDays);

        Assert.Equal(
            "2026-08-23",
            pacus.LastGrowthDate);

        var log =
            await growthRepo.GetByUserAndDateAsync(
                userId,
                "2026-08-23");

        Assert.NotNull(log);
    }

    [Fact]
    public async Task DiaCompleto_CresceIgualAoDiaZerado()
    {
        var templates =
            new List<TaskTemplate>
            {
                new()
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = ObjectId.Empty,
                    Title = "Escovar dentes",
                    Type = TaskType.Mandatory,
                    Period = TaskPeriod.Morning,
                    Points = 1,
                    Order = 1,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
            };

        var userId =
            ObjectId.GenerateNewId();

        templates[0].UserId =
            userId;

        var (
            dayClosing,
            dailyRoutine,
            routines,
            pacusRepo,
            _,
            pointsRepo) = BuildSystem(
                templates);

        await pacusRepo.CreateAsync(
            NewPacus(userId));

        var routine =
            await dailyRoutine.CreateRoutineForDateAsync(
                userId,
                "2026-08-23",
                "America/Sao_Paulo");

        await dailyRoutine.ToggleTaskAsync(
            userId,
            routine.Tasks[0].Id,
            true,
            userId,
            "child");

        await CloseUpTo(
            dayClosing,
            userId,
            "2026-08-24");

        var pacus =
            await pacusRepo.GetByFamilyIdAsync(
                userId);

        Assert.Equal(
            1,
            pacus!.TotalClosedDays);

        var closed =
            await routines.GetByUserAndDateAsync(
                userId,
                "2026-08-23");

        Assert.Equal(
            1,
            closed!.PointsEarned);
    }

    [Fact]
    public async Task CrescimentoDuplicado_NaoCresceDuasVezesNoMesmoDia()
    {
        var (
            dayClosing,
            dailyRoutine,
            routines,
            pacusRepo,
            growthRepo,
            _) = BuildSystem();

        var userId =
            ObjectId.GenerateNewId();

        await pacusRepo.CreateAsync(
            NewPacus(userId));

        await dailyRoutine.CreateRoutineForDateAsync(
            userId,
            "2026-08-23",
            "America/Sao_Paulo");

        // Chama o fechamento duas vezes para a mesma janela: simula reentrada/corrida.
        await CloseUpTo(
            dayClosing,
            userId,
            "2026-08-24");

        await dayClosing.CloseIfDueAsync(
            userId,
            "America/Sao_Paulo");

        var pacus =
            await pacusRepo.GetByFamilyIdAsync(
                userId);

        Assert.Equal(
            1,
            pacus!.TotalClosedDays);

        Assert.Single(
            growthRepo.Logs.Where(
                l => l.UserId == userId));
    }

    [Fact]
    public async Task ReversaoDeTarefa_DesmarcarDevolveExatamenteOsPontosGanhos()
    {
        var templates =
            new List<TaskTemplate>
            {
                new()
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = ObjectId.Empty,
                    Title = "Ler livro",
                    Type = TaskType.Expected,
                    Period = TaskPeriod.Evening,
                    Points = 3,
                    Order = 1,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
            };

        var userId =
            ObjectId.GenerateNewId();

        templates[0].UserId =
            userId;

        var (
            _,
            dailyRoutine,
            _,
            _,
            _,
            pointsRepo) = BuildSystem(
                templates);

        var routine =
            await dailyRoutine.CreateRoutineForDateAsync(
                userId,
                "2026-08-24",
                "America/Sao_Paulo");

        var taskId =
            routine.Tasks[0].Id;

        await dailyRoutine.ToggleTaskAsync(
            userId,
            taskId,
            true,
            userId,
            "child");

        Assert.Equal(
            3,
            await pointsRepo.GetBalanceAsync(
                userId));

        await dailyRoutine.ToggleTaskAsync(
            userId,
            taskId,
            false,
            userId,
            "child");

        Assert.Equal(
            0,
            await pointsRepo.GetBalanceAsync(
                userId));

        Assert.Equal(
            2,
            pointsRepo.Transactions.Count(
                t => t.UserId == userId));
    }

    [Fact]
    public async Task PontosDuplicados_MarcarDuasVezesSeguidasNaoDuplicaPontos()
    {
        var templates =
            new List<TaskTemplate>
            {
                new()
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = ObjectId.Empty,
                    Title = "Beber agua",
                    Type = TaskType.Mandatory,
                    Period = TaskPeriod.Morning,
                    Points = 2,
                    Order = 1,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
            };

        var userId =
            ObjectId.GenerateNewId();

        templates[0].UserId =
            userId;

        var (
            _,
            dailyRoutine,
            _,
            _,
            _,
            pointsRepo) = BuildSystem(
                templates);

        var routine =
            await dailyRoutine.CreateRoutineForDateAsync(
                userId,
                "2026-08-24",
                "America/Sao_Paulo");

        var taskId =
            routine.Tasks[0].Id;

        // Marcar -> ganhar -> marcar de novo (sem desmarcar) nao deve fabricar pontos.
        await dailyRoutine.ToggleTaskAsync(
            userId,
            taskId,
            true,
            userId,
            "child");

        await dailyRoutine.ToggleTaskAsync(
            userId,
            taskId,
            true,
            userId,
            "child");

        Assert.Equal(
            2,
            await pointsRepo.GetBalanceAsync(
                userId));

        Assert.Single(
            pointsRepo.Transactions.Where(
                t => t.UserId == userId));
    }

    [Fact]
    public async Task NovoDia_TarefasComecamPendentes()
    {
        var templates =
            new List<TaskTemplate>
            {
                new()
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = ObjectId.Empty,
                    Title = "Arrumar cama",
                    Type = TaskType.Mandatory,
                    Period = TaskPeriod.Morning,
                    Points = 1,
                    Order = 1,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
            };

        var userId =
            ObjectId.GenerateNewId();

        templates[0].UserId =
            userId;

        var (
            _,
            dailyRoutine,
            _,
            _,
            _,
            _) = BuildSystem(
                templates);

        var routine =
            await dailyRoutine.CreateRoutineForDateAsync(
                userId,
                "2026-08-24",
                "America/Sao_Paulo");

        Assert.All(
            routine.Tasks,
            t => Assert.Equal(
                TaskItemStatus.Pending,
                t.Status));
    }

    // O clock fixo injetado em BuildSystem ja fixa "hoje" em 2026-08-24 (UTC).
    // Este helper documenta a intencao no corpo dos testes.
    private static Task CloseUpTo(
        DayClosingService dayClosing,
        ObjectId userId,
        string simulatedToday) =>
        dayClosing.CloseIfDueAsync(
            userId,
            "UTC");
}