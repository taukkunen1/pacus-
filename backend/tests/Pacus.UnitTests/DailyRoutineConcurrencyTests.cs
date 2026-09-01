using MongoDB.Bson;
using Pacus.Application.Exceptions;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cobre a concorrencia otimista em DailyRoutineRepository.UpdateAsync (achado #5 da
// auditoria de API de 2026-09-01, ver docs/ESTADO_ATUAL.md): antes, duas requisicoes
// mexendo na mesma rotina ao mesmo tempo (ex.: crianca completando uma tarefa e adulto
// ajustando o game timer quase juntos) causavam um "lost update" silencioso -- a segunda
// gravacao sobrescrevia a primeira sem erro nenhum. Agora a segunda gravacao, baseada em
// uma leitura desatualizada, falha com ConflictException (409) em vez de apagar a
// mudanca de quem gravou primeiro.
public class DailyRoutineConcurrencyTests
{
    private static (FakeDailyRoutineRepository routines, DailyRoutineService dailyRoutine)
        BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templateRepo = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        var dailyRoutine = new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
        return (routines, dailyRoutine);
    }

    [Fact]
    public async Task UpdateAsync_LeituraUnica_IncrementaVersaoENaoLancaExcecao()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        var routine = await routines.GetByUserAndDateAsync(userId, "2026-09-01");

        Assert.NotNull(routine);
        Assert.Equal(0, routine!.Version);

        routine.GameTimerExtraMinutes = 30;
        await routines.UpdateAsync(routine);

        Assert.Equal(1, routine.Version);

        var reloaded = await routines.GetByUserAndDateAsync(userId, "2026-09-01");
        Assert.Equal(1, reloaded!.Version);
        Assert.Equal(30, reloaded.GameTimerExtraMinutes);
    }

    [Fact]
    public async Task UpdateAsync_DuasLeiturasConcorrentes_SegundaGravacaoLancaConflictException()
    {
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        // Duas "requisicoes" leem a mesma rotina antes de qualquer uma delas gravar --
        // cada leitura devolve uma copia independente, igual um round-trip real com o
        // Mongo (ver FakeDailyRoutineRepository.Clone).
        var readByChild = await routines.GetByUserAndDateAsync(userId, "2026-09-01");
        var readByAdult = await routines.GetByUserAndDateAsync(userId, "2026-09-01");

        readByChild!.GameTimerExtraMinutes = 10;
        readByAdult!.GameTimerExtraMinutes = 60;

        // A crianca grava primeiro -- sucesso, versao vai de 0 para 1.
        await routines.UpdateAsync(readByChild);

        // O adulto grava em cima da mesma versao (0) que ja nao existe mais -- em vez de
        // sobrescrever silenciosamente o que a crianca gravou, falha alto.
        await Assert.ThrowsAsync<ConflictException>(() =>
            routines.UpdateAsync(readByAdult));

        // O que ficou salvo e o que a crianca gravou primeiro, intacto.
        var final = await routines.GetByUserAndDateAsync(userId, "2026-09-01");
        Assert.Equal(10, final!.GameTimerExtraMinutes);
    }

    [Fact]
    public async Task CreateAsync_NaoAfetaVersaoDaCopiaDevolvida()
    {
        // CreateAsync devolve a mesma referencia que o chamador passou (igual o
        // repositorio real, que so faz InsertOneAsync e devolve o objeto de volta) --
        // confirma que a primeira gravacao seguinte, feita direto em cima desse objeto,
        // ainda enxerga Version = 0 e funciona normalmente.
        var (routines, dailyRoutine) = BuildSystem();
        var userId = ObjectId.GenerateNewId();

        var created = await dailyRoutine.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");
        Assert.Equal(0, created.Version);

        created.GameTimerExtraMinutes = 5;
        await routines.UpdateAsync(created);

        Assert.Equal(1, created.Version);
    }
}
