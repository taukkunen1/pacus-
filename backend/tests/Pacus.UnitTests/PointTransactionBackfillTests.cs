using MongoDB.Bson;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

public class PointTransactionBackfillTests
{
    [Fact]
    public async Task LegacyTransactions_BackfillSourceReferences_IsIdempotent()
    {
        var repo = new FakePointTransactionRepository();
        repo.Transactions.Add(new PointTransaction
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = ObjectId.GenerateNewId(),
            TaskId = "task-legacy",
            TaskTitle = "Legado",
            Type = PointTransactionType.Award,
            Points = 10,
            SourceType = string.Empty,
            SourceId = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });

        Assert.Equal(1, await repo.BackfillSourceReferencesAsync());
        Assert.Equal("task", repo.Transactions[0].SourceType);
        Assert.Equal("task-legacy", repo.Transactions[0].SourceId);
        Assert.Equal(0, await repo.BackfillSourceReferencesAsync());
    }

    [Fact]
    public async Task LegacyAdjustment_UsesStableFallbackReference_WhenTaskIdMissing()
    {
        var repo = new FakePointTransactionRepository();
        var id = ObjectId.GenerateNewId();
        repo.Transactions.Add(new PointTransaction
        {
            Id = id,
            FamilyId = ObjectId.GenerateNewId(),
            TaskId = string.Empty,
            TaskTitle = "Ajuste legado",
            Type = PointTransactionType.Adjustment,
            Points = 20,
            CreatedAt = DateTime.UtcNow,
        });

        await repo.BackfillSourceReferencesAsync();

        Assert.Equal("adjustment", repo.Transactions[0].SourceType);
        Assert.Equal(id.ToString(), repo.Transactions[0].SourceId);
    }
}
