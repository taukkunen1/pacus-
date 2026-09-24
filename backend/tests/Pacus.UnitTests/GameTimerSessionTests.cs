using MongoDB.Bson;
using Pacus.Application.Exceptions;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

public class GameTimerSessionTests
{
    private static (FakeDailyRoutineRepository Routines, DailyRoutineService Service) BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templates = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var points = new PointsService(new FakePointTransactionRepository());
        var service = new DailyRoutineService(
            routines, templates, events, points, new FakeSettingsRepository());
        return (routines, service);
    }

    [Fact]
    public async Task StartSession_ReservaSaldoEGravaEstadoNoServidor()
    {
        var (routines, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");

        var started = await service.StartGameTimerSessionAsync(
            familyId, 30, actorId, "child");

        Assert.Equal(-30, started.GameTimerExtraMinutes);
        Assert.Equal(30, started.GameTimerSessionMinutes);
        Assert.NotNull(started.GameTimerSessionEndsAt);
        Assert.Null(started.GameTimerSessionRemainingSeconds);

        var persisted = await routines.GetLatestOpenAsync(familyId);
        Assert.NotNull(persisted);
        Assert.Equal(-30, persisted!.GameTimerExtraMinutes);
        Assert.Equal(30, persisted.GameTimerSessionMinutes);
        Assert.NotNull(persisted.GameTimerSessionEndsAt);
    }

    [Fact]
    public async Task PauseEResume_PersistemTempoRestante()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");
        await service.StartGameTimerSessionAsync(
            familyId, 30, actorId, "child");

        var paused = await service.PauseGameTimerSessionAsync(
            familyId, actorId, "child");

        Assert.Null(paused.GameTimerSessionEndsAt);
        Assert.NotNull(paused.GameTimerSessionRemainingSeconds);
        Assert.InRange(paused.GameTimerSessionRemainingSeconds!.Value, 1, 1800);

        var resumed = await service.ResumeGameTimerSessionAsync(
            familyId, actorId, "child");

        Assert.NotNull(resumed.GameTimerSessionEndsAt);
        Assert.Null(resumed.GameTimerSessionRemainingSeconds);
        Assert.Equal(30, resumed.GameTimerSessionMinutes);
    }

    [Fact]
    public async Task FinishSession_NaoDebitaSaldoDuasVezes()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");

        var started = await service.StartGameTimerSessionAsync(
            familyId, 60, actorId, "child");
        Assert.Equal(-60, started.GameTimerExtraMinutes);

        var finished = await service.FinishGameTimerSessionAsync(
            familyId, actorId, "child");

        Assert.Equal(-60, finished.GameTimerExtraMinutes);
        Assert.Null(finished.GameTimerSessionMinutes);
        Assert.Null(finished.GameTimerSessionEndsAt);
        Assert.Null(finished.GameTimerSessionRemainingSeconds);
    }

    [Fact]
    public async Task CancelSession_DevolveSomenteTempoNaoUsadoEVoltaAoSaldo()
    {
        var (routines, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");

        var started = await service.StartGameTimerSessionAsync(
            familyId, 60, actorId, "child");
        Assert.Equal(-60, started.GameTimerExtraMinutes);

        // Simula 30m01s usados: restam 29m59s. O saldo trabalha em minutos,
        // entao os 29m59s restantes devolvem 30 minutos.
        started.GameTimerSessionEndsAt = null;
        started.GameTimerSessionRemainingSeconds = 1799;
        await routines.UpdateAsync(started);

        var cancelled = await service.CancelGameTimerSessionAsync(
            familyId, actorId, "child");

        Assert.Equal(-30, cancelled.GameTimerExtraMinutes);
        Assert.Null(cancelled.GameTimerSessionMinutes);
        Assert.Null(cancelled.GameTimerSessionEndsAt);
        Assert.Null(cancelled.GameTimerSessionRemainingSeconds);
    }

    [Fact]
    public async Task CancelSession_QuaseNoInicio_DevolveMinutoInteiroReservado()
    {
        var (routines, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");

        var started = await service.StartGameTimerSessionAsync(
            familyId, 60, actorId, "child");

        // Caso da UI mostrado pelo usuario: 59:58 restantes.
        started.GameTimerSessionEndsAt = null;
        started.GameTimerSessionRemainingSeconds = 3598;
        await routines.UpdateAsync(started);

        var cancelled = await service.CancelGameTimerSessionAsync(
            familyId, actorId, "child");

        Assert.Equal(0, cancelled.GameTimerExtraMinutes);
        Assert.Null(cancelled.GameTimerSessionMinutes);
    }

    [Fact]
    public async Task StartSession_ComSessaoAtiva_RejeitaSegundaSessao()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");
        await service.StartGameTimerSessionAsync(
            familyId, 15, actorId, "child");

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.StartGameTimerSessionAsync(
                familyId, 15, actorId, "child"));
    }

    [Fact]
    public async Task StartSession_AposSessaoExpirada_NormalizaEIniciaNova()
    {
        var (routines, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(
            familyId, "2026-09-24", "America/Sao_Paulo");
        var first = await service.StartGameTimerSessionAsync(
            familyId, 30, actorId, "child");

        first.GameTimerSessionEndsAt = DateTime.UtcNow.AddSeconds(-1);
        await routines.UpdateAsync(first);

        var second = await service.StartGameTimerSessionAsync(
            familyId, 15, actorId, "child");

        Assert.Equal(-45, second.GameTimerExtraMinutes);
        Assert.Equal(15, second.GameTimerSessionMinutes);
        Assert.NotNull(second.GameTimerSessionEndsAt);
    }
    [Fact]
    public async Task PauseEResume_Repetidos_SaoIdempotentes()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-09-24", "America/Sao_Paulo");
        await service.StartGameTimerSessionAsync(familyId, 30, actorId, "child");

        var paused1 = await service.PauseGameTimerSessionAsync(familyId, actorId, "child");
        var paused2 = await service.PauseGameTimerSessionAsync(familyId, actorId, "child");
        Assert.Equal(paused1.GameTimerSessionRemainingSeconds, paused2.GameTimerSessionRemainingSeconds);

        var resumed1 = await service.ResumeGameTimerSessionAsync(familyId, actorId, "child");
        var resumed2 = await service.ResumeGameTimerSessionAsync(familyId, actorId, "child");
        Assert.Equal(resumed1.GameTimerSessionMinutes, resumed2.GameTimerSessionMinutes);
        Assert.NotNull(resumed2.GameTimerSessionEndsAt);
    }

    [Fact]
    public async Task Cancel_Repetido_NaoDevolveSaldoDuasVezes()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-09-24", "America/Sao_Paulo");
        await service.StartGameTimerSessionAsync(familyId, 60, actorId, "child");

        var first = await service.CancelGameTimerSessionAsync(familyId, actorId, "child");
        var balanceAfterFirst = first.GameTimerExtraMinutes;
        var second = await service.CancelGameTimerSessionAsync(familyId, actorId, "child");

        Assert.Equal(balanceAfterFirst, second.GameTimerExtraMinutes);
        Assert.Null(second.GameTimerSessionMinutes);
    }

    [Fact]
    public async Task StartSession_PersisteHorarioInicial()
    {
        var (_, service) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();
        await service.CreateRoutineForDateAsync(familyId, "2026-09-24", "America/Sao_Paulo");

        var started = await service.StartGameTimerSessionAsync(familyId, 15, actorId, "child");

        Assert.NotNull(started.GameTimerSessionStartedAt);
        Assert.NotNull(started.GameTimerSessionEndsAt);
        Assert.True(started.GameTimerSessionEndsAt > started.GameTimerSessionStartedAt);
    }

}
