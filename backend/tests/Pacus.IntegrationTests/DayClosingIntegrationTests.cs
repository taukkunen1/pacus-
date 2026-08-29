using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Infrastructure.Mongo;
using Pacus.Infrastructure.Repositories;

namespace Pacus.IntegrationTests;

public class DayClosingIntegrationTests : IClassFixture<MongoIntegrationFixture>
{
    private readonly MongoIntegrationFixture _fixture;

    public DayClosingIntegrationTests(MongoIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CloseIfDueAsync_ShouldCloseRoutineGrowPacusAndBeIdempotent()
    {
        var databaseName = $"pacus_test_{Guid.NewGuid():N}";

        var context = new MongoDbContext(
            Options.Create(
                new MongoDbSettings
                {
                    ConnectionString = _fixture.ConnectionString,
                    DatabaseName = databaseName
                }));

        var routineRepository = new DailyRoutineRepository(context);
        var pacusRepository = new PacusRepository(context);
        var growthRepository = new PacusGrowthRepository(context);
        var settingsRepository = new SettingsRepository(context);
        var taskTemplateRepository = new TaskTemplateRepository(context);
        var taskEventRepository = new TaskEventRepository(context);
        var pointsRepository = new PointTransactionRepository(context);
        var pointsService = new PointsService(pointsRepository);

        var taskService = new DailyRoutineService(
            routineRepository,
            taskTemplateRepository,
            taskEventRepository,
            pointsService,
            settingsRepository);

        var userId = ObjectId.GenerateNewId();
        var pacusId = ObjectId.GenerateNewId();

        var yesterday = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow.AddDays(-1));

        var today = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow);

        await context.Pacus.InsertOneAsync(
            new Pacus.Domain.Entities.Pacus
            {
                Id = pacusId,
                FamilyId = userId,
                Name = "Pacus",
                Species = "axolotl",
                BirthDate = DateTime.UtcNow.AddDays(-1),
                Stage = PacusStage.Egg,
                StageHistory = new List<PacusStageHistoryEntry>(),
                Size = 1.0,
                TotalClosedDays = 0,
                LastGrowthDate = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await routineRepository.CreateAsync(
            new DailyRoutine
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = userId,
                Date = yesterday,
                Timezone = "America/Sao_Paulo",
                Status = RoutineStatus.Open,
                Tasks = new List<DailyTask>(),
                Stats = new DailyRoutineStats
                {
                    Mandatory = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    Expected = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    Challenge = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    PointsEarned = 0,
                    CompletionRate = 0
                },
                PointsEarned = 0,
                ClosedAt = null,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

        var closingService = new DayClosingService(
            routineRepository,
            taskService,
            pacusRepository,
            growthRepository,
            settingsRepository,
            new FixedClock(DateTime.UtcNow));

        await closingService.CloseIfDueAsync(
            userId,
            "America/Sao_Paulo");

        var closedRoutine =
            await routineRepository.GetByUserAndDateAsync(
                userId,
                yesterday);

        Assert.NotNull(closedRoutine);
        Assert.Equal(RoutineStatus.Closed, closedRoutine!.Status);
        Assert.NotNull(closedRoutine.ClosedAt);

        var grownPacus =
            await pacusRepository.GetByFamilyIdAsync(userId);

        Assert.NotNull(grownPacus);
        Assert.Equal(1, grownPacus!.TotalClosedDays);
        Assert.Equal(yesterday, grownPacus.LastGrowthDate);
        Assert.Equal(1.0, grownPacus.Size, precision: 2);

        var growthLog =
            await growthRepository.GetByUserAndDateAsync(
                userId,
                yesterday);

        Assert.NotNull(growthLog);
        Assert.Equal(pacusId, growthLog!.PacusId);
        Assert.Equal(yesterday, growthLog.Date);
        Assert.Equal(1.0, growthLog.SizeBefore, precision: 2);
        Assert.Equal(1.0, growthLog.SizeAfter, precision: 2);

        var todayRoutine =
            await routineRepository.GetByUserAndDateAsync(
                userId,
                today);

        Assert.NotNull(todayRoutine);
        Assert.Equal(RoutineStatus.Open, todayRoutine!.Status);

        var daysBeforeSecondCall =
            grownPacus.TotalClosedDays;

        var sizeBeforeSecondCall =
            grownPacus.Size;

        var lastGrowthDateBeforeSecondCall =
            grownPacus.LastGrowthDate;

        await closingService.CloseIfDueAsync(
            userId,
            "America/Sao_Paulo");

        var pacusAfterSecondCall =
            await pacusRepository.GetByFamilyIdAsync(userId);

        Assert.NotNull(pacusAfterSecondCall);
        Assert.Equal(
            daysBeforeSecondCall,
            pacusAfterSecondCall!.TotalClosedDays);

        Assert.Equal(
            sizeBeforeSecondCall,
            pacusAfterSecondCall.Size);

        Assert.Equal(
            lastGrowthDateBeforeSecondCall,
            pacusAfterSecondCall.LastGrowthDate);

        var logs =
            await context.PacusGrowthLogs
                .Find(
                    l =>
                        l.UserId == userId &&
                        l.Date == yesterday)
                .ToListAsync();

        Assert.Single(logs);
    }

    [Fact]
    public async Task CloseIfDueAsync_ShouldProcessMultipleMissedDaysIndividually()
    {
        var databaseName = $"pacus_test_{Guid.NewGuid():N}";

        var context = new MongoDbContext(
            Options.Create(
                new MongoDbSettings
                {
                    ConnectionString = _fixture.ConnectionString,
                    DatabaseName = databaseName
                }));

        var routineRepository = new DailyRoutineRepository(context);
        var pacusRepository = new PacusRepository(context);
        var growthRepository = new PacusGrowthRepository(context);
        var settingsRepository = new SettingsRepository(context);
        var taskTemplateRepository = new TaskTemplateRepository(context);
        var taskEventRepository = new TaskEventRepository(context);
        var pointsRepository = new PointTransactionRepository(context);
        var pointsService = new PointsService(pointsRepository);

        var taskService = new DailyRoutineService(
            routineRepository,
            taskTemplateRepository,
            taskEventRepository,
            pointsService,
            settingsRepository);

        var userId = ObjectId.GenerateNewId();
        var pacusId = ObjectId.GenerateNewId();

        var today = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow);

        var missed1 = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow.AddDays(-3));

        var missed2 = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow.AddDays(-2));

        var missed3 = TimezoneHelper.GetOperationalDate(
            "America/Sao_Paulo",
            DateTime.UtcNow.AddDays(-1));

        await context.Pacus.InsertOneAsync(
            new Pacus.Domain.Entities.Pacus
            {
                Id = pacusId,
                FamilyId = userId,
                Name = "Pacus",
                Species = "axolotl",
                BirthDate = DateTime.UtcNow.AddDays(-10),
                Stage = PacusStage.Baby,
                StageHistory = new List<PacusStageHistoryEntry>(),
                Size = 1.0,
                TotalClosedDays = 0,
                LastGrowthDate = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await routineRepository.CreateAsync(
            new DailyRoutine
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = userId,
                Date = missed1,
                Timezone = "America/Sao_Paulo",
                Status = RoutineStatus.Open,
                Tasks = new List<DailyTask>(),
                Stats = new DailyRoutineStats
                {
                    Mandatory = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    Expected = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    Challenge = new TaskTypeStat
                    {
                        Done = 0,
                        Total = 0
                    },
                    PointsEarned = 0,
                    CompletionRate = 0
                },
                PointsEarned = 0,
                ClosedAt = null,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });

        var closingService = new DayClosingService(
            routineRepository,
            taskService,
            pacusRepository,
            growthRepository,
            settingsRepository,
            new FixedClock(DateTime.UtcNow));

        await closingService.CloseIfDueAsync(
            userId,
            "America/Sao_Paulo");

        foreach (var date in new[] { missed1, missed2, missed3 })
        {
            var routine =
                await routineRepository.GetByUserAndDateAsync(
                    userId,
                    date);

            Assert.NotNull(routine);
            Assert.Equal(RoutineStatus.Closed, routine!.Status);
            Assert.NotNull(routine.ClosedAt);
        }

        var todayRoutine =
            await routineRepository.GetByUserAndDateAsync(
                userId,
                today);

        Assert.NotNull(todayRoutine);
        Assert.Equal(RoutineStatus.Open, todayRoutine!.Status);

        var updatedPacus =
            await pacusRepository.GetByFamilyIdAsync(userId);

        Assert.NotNull(updatedPacus);
        Assert.Equal(3, updatedPacus!.TotalClosedDays);
        Assert.Equal(missed3, updatedPacus.LastGrowthDate);

        var logs =
            await context.PacusGrowthLogs
                .Find(l => l.UserId == userId)
                .SortBy(l => l.Date)
                .ToListAsync();

        Assert.Equal(3, logs.Count);

        Assert.Equal(
            new[] { missed1, missed2, missed3 },
            logs.Select(l => l.Date).ToArray());
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}

