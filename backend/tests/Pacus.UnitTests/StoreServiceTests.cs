using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

public class StoreServiceTests
{
    private static (StoreService store, FakeStoreRepository storeRepo, FakePointTransactionRepository pointsRepo,
        FakeDailyRoutineRepository routines)
        BuildSystem()
    {
        var storeRepo = new FakeStoreRepository();
        var pointsRepo = new FakePointTransactionRepository();
        var pointsService = new PointsService(pointsRepo);
        var auditLogRepo = new FakeAuditLogRepository();

        // Usado somente quando um item concede tempo de tela (ScreenTimeMinutes) --
        // nenhum teste existente seta esse campo, entao esta dependencia fica ociosa
        // ate os testes novos de "1 hora de tela" que a usam de proposito.
        var routines = new FakeDailyRoutineRepository();
        var dailyRoutineService = new DailyRoutineService(
            routines,
            new FakeTaskTemplateRepository(),
            new FakeTaskEventRepository(),
            new PointsService(new FakePointTransactionRepository()),
            new FakeSettingsRepository());

        var store = new StoreService(storeRepo, pointsService, auditLogRepo, dailyRoutineService);
        return (store, storeRepo, pointsRepo, routines);
    }

    private static async Task GivePoints(FakePointTransactionRepository pointsRepo, ObjectId familyId, int amount)
    {
        var pointsService = new PointsService(pointsRepo);
        await pointsService.RecordAsync(familyId, null, "2026-08-24", "seed", "saldo inicial",
            PointTransactionType.Award, amount, familyId, UserRole.Adult);
    }

    [Fact]
    public async Task AprovarResgate_DebitaOSaldoExatamente()
    {
        var (store, storeRepo, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 500);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("Carrinho Hot Wheels", null, 300, "toy", null, 1));

        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        Assert.Equal(RedemptionStatus.Pending, redemption.Status);

        var approved = await store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        Assert.Equal(RedemptionStatus.Approved, approved.Status);
        Assert.Equal(200, await pointsRepo.GetBalanceAsync(familyId)); // 500 - 300
    }

    [Fact]
    public async Task AprovarResgate_SaldoInsuficiente_LancaExcecaoENaoDebitaEstoque()
    {
        var (store, storeRepo, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 100); // menos que o custo do item

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("Carrinho Hot Wheels", null, 300, "toy", null, 1));
        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId));

        Assert.Equal(100, await pointsRepo.GetBalanceAsync(familyId)); // saldo intacto
        var stillItem = await storeRepo.GetItemByIdAsync(item.Id);
        Assert.Equal(1, stillItem!.Stock); // estoque intacto
    }

    [Fact]
    public async Task RejeitarResgate_NaoDebitaPontos()
    {
        var (store, _, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 500);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de TV", null, 100, "screen_time", null, null));
        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);

        var rejected = await store.RejectRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        Assert.Equal(RedemptionStatus.Rejected, rejected.Status);
        Assert.Equal(500, await pointsRepo.GetBalanceAsync(familyId));
    }

    [Fact]
    public async Task ResgateJaRevisado_NaoPodeSerRevisadoDeNovo()
    {
        var (store, _, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 500);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de TV", null, 100, "screen_time", null, null));
        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);

        await store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RejectRedemptionAsync(familyId, redemption.Id.ToString(), familyId));
    }

    [Fact]
    public async Task EstoqueFinito_ZeraEDesativaItemAposResgateAprovado()
    {
        var (store, storeRepo, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 1000);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("Carrinho Hot Wheels", null, 300, "toy", null, 1));
        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        await store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        var updatedItem = await storeRepo.GetItemByIdAsync(item.Id);
        Assert.Equal(0, updatedItem!.Stock);
        Assert.False(updatedItem.Active);
    }

    [Fact]
    public async Task EstoqueIlimitado_NaoEDescontadoNemDesativado()
    {
        var (store, storeRepo, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 500);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de TV", null, 100, "screen_time", null, null));
        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        await store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        var updatedItem = await storeRepo.GetItemByIdAsync(item.Id);
        Assert.Null(updatedItem!.Stock);
        Assert.True(updatedItem.Active);
    }

    // "1 hora de tela = 100 pontos, limite 1x resgate por dia" (pedido do dono do produto).
    [Fact]
    public async Task ItemComLimiteDiario_ImpedeSegundoResgateNoMesmoDiaOperacional()
    {
        var (store, _, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 1000);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de tela", null, 100, "screen_time", "🎮", null, DailyLimit: 1, ScreenTimeMinutes: 60));

        var first = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        Assert.Equal(RedemptionStatus.Pending, first.Status);

        // Nem precisa esperar a revisao do adulto -- a segunda SOLICITACAO no mesmo dia
        // ja e bloqueada, mesmo com a primeira ainda Pending.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RequestRedemptionAsync(familyId, familyId, item.Id));
        Assert.Contains("Limite diario", ex.Message);
    }

    [Fact]
    public async Task ItemComLimiteDiario_ResgateRejeitadoNaoContaParaOLimite()
    {
        var (store, _, pointsRepo, _) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 1000);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de tela", null, 100, "screen_time", "🎮", null, DailyLimit: 1, ScreenTimeMinutes: 60));

        var first = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        await store.RejectRedemptionAsync(familyId, first.Id.ToString(), familyId);

        // Rejeitado nao consome a vaga do dia -- uma nova solicitacao deve funcionar.
        var second = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        Assert.Equal(RedemptionStatus.Pending, second.Status);
    }

    // "retire os pacus points utilizados" -- aprovar um item que concede tempo de tela
    // credita os minutos direto no game timer do dia (mesmo mecanismo dos botoes +5/-5
    // min do adulto em DailyRoutinesController), alem de debitar o saldo (ja coberto
    // pelos testes acima).
    [Fact]
    public async Task ItemComTempoDeTela_ConcedeMinutosNoGameTimerAoAprovar()
    {
        var (store, _, pointsRepo, routines) = BuildSystem();
        var familyId = ObjectId.GenerateNewId();
        await GivePoints(pointsRepo, familyId, 500);

        var item = await store.CreateItemAsync(familyId, familyId,
            new CreateStoreItemRequest("1 hora de tela", null, 100, "screen_time", "🎮", null, DailyLimit: 1, ScreenTimeMinutes: 60));

        var redemption = await store.RequestRedemptionAsync(familyId, familyId, item.Id);
        await store.ApproveRedemptionAsync(familyId, redemption.Id.ToString(), familyId);

        var today = Pacus.Application.Utils.TimezoneHelper.GetOperationalDate("America/Sao_Paulo");
        var routine = await routines.GetByUserAndDateAsync(familyId, today);
        Assert.Equal(60, routine!.GameTimerExtraMinutes);
    }
}
