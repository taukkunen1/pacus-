using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Services;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

public class StoreServiceTests
{
    private static (StoreService store, FakeStoreRepository storeRepo, FakePointTransactionRepository pointsRepo)
        BuildSystem()
    {
        var storeRepo = new FakeStoreRepository();
        var pointsRepo = new FakePointTransactionRepository();
        var pointsService = new PointsService(pointsRepo);
        var store = new StoreService(storeRepo, pointsService);
        return (store, storeRepo, pointsRepo);
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
        var (store, storeRepo, pointsRepo) = BuildSystem();
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
        var (store, storeRepo, pointsRepo) = BuildSystem();
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
        var (store, _, pointsRepo) = BuildSystem();
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
        var (store, _, pointsRepo) = BuildSystem();
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
        var (store, storeRepo, pointsRepo) = BuildSystem();
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
        var (store, storeRepo, pointsRepo) = BuildSystem();
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
}
