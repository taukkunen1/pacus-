using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cobre a reacao pessoal do adulto sobre o dia (relatedness -- terceira necessidade
// da Teoria da Autodeterminacao, ver docs/PROPOSITO.md e Domain/Entities/DailyReaction.cs).
public class DailyReactionTests
{
    private static DailyRoutineService BuildSystem()
    {
        var routines = new FakeDailyRoutineRepository();
        var templateRepo = new FakeTaskTemplateRepository();
        var events = new FakeTaskEventRepository();
        var pointsService = new PointsService(new FakePointTransactionRepository());
        return new DailyRoutineService(
            routines, templateRepo, events, pointsService, new FakeSettingsRepository());
    }

    [Fact]
    public async Task SetReactionAsync_AdultoComIconeValido_GravaReacao()
    {
        var service = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        var updated = await service.SetReactionAsync(
            userId, "heart", "Fiquei muito orgulhoso de você hoje.", actorId, "adult");

        Assert.NotNull(updated.Reaction);
        Assert.Equal("heart", updated.Reaction!.Icon);
        Assert.Equal("Fiquei muito orgulhoso de você hoje.", updated.Reaction.Message);
        Assert.Equal(actorId, updated.Reaction.CreatedBy);
    }

    [Fact]
    public async Task SetReactionAsync_SemMensagem_GravaSoIcone()
    {
        var service = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        var updated = await service.SetReactionAsync(userId, "clap", "   ", actorId, "adult");

        Assert.Equal("clap", updated.Reaction!.Icon);
        Assert.Null(updated.Reaction.Message);
    }

    [Fact]
    public async Task SetReactionAsync_IconeInvalido_LancaExcecao()
    {
        var service = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetReactionAsync(userId, "fogo", null, actorId, "adult"));
    }

    [Fact]
    public async Task SetReactionAsync_Crianca_LancaUnauthorized()
    {
        var service = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SetReactionAsync(userId, "heart", null, actorId, "child"));
    }

    [Fact]
    public async Task SetReactionAsync_ChamadoDeNovoNoMesmoDia_SubstituiReacaoAnterior()
    {
        var service = BuildSystem();
        var userId = ObjectId.GenerateNewId();
        var actorId = ObjectId.GenerateNewId();

        await service.CreateRoutineForDateAsync(userId, "2026-09-01", "America/Sao_Paulo");

        await service.SetReactionAsync(userId, "heart", "Primeira reação.", actorId, "adult");
        var updated = await service.SetReactionAsync(userId, "star", "Segunda reação.", actorId, "adult");

        Assert.Equal("star", updated.Reaction!.Icon);
        Assert.Equal("Segunda reação.", updated.Reaction.Message);
    }
}
