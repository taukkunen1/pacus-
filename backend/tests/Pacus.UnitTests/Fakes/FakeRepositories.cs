using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.UnitTests.Fakes;

// Fakes em memoria — permitem testar os services sem depender de um MongoDB real.

public class FakeDailyRoutineRepository : IDailyRoutineRepository
{
    private readonly List<DailyRoutine> _routines = new();

    public Task<DailyRoutine?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        Task.FromResult(_routines.FirstOrDefault(r => r.UserId == userId && r.Date == date));

    public Task<DailyRoutine> CreateAsync(DailyRoutine routine)
    {
        _routines.Add(routine);
        return Task.FromResult(routine);
    }

    public Task UpdateAsync(DailyRoutine routine)
    {
        var index = _routines.FindIndex(r => r.Id == routine.Id);
        if (index >= 0) _routines[index] = routine;
        return Task.CompletedTask;
    }

    public Task<List<DailyRoutine>> GetHistoryAsync(ObjectId userId, string? from, string? to)
    {
        var query = _routines.Where(r => r.UserId == userId && r.Status == RoutineStatus.Closed);
        if (from is not null) query = query.Where(r => string.Compare(r.Date, from, StringComparison.Ordinal) >= 0);
        if (to is not null) query = query.Where(r => string.Compare(r.Date, to, StringComparison.Ordinal) <= 0);
        return Task.FromResult(query.OrderByDescending(r => r.Date).ToList());
    }

    public Task<DailyRoutine?> GetLatestOpenAsync(ObjectId userId) =>
        Task.FromResult(_routines
            .Where(r => r.UserId == userId && r.Status == RoutineStatus.Open)
            .OrderByDescending(r => r.Date)
            .FirstOrDefault());
}

public class FakeTaskTemplateRepository : ITaskTemplateRepository
{
    private readonly List<TaskTemplate> _templates;

    public FakeTaskTemplateRepository(List<TaskTemplate>? seed = null) => _templates = seed ?? new();

    public Task<List<TaskTemplate>> GetActiveByUserAsync(ObjectId userId) =>
        Task.FromResult(_templates.Where(t => t.UserId == userId && t.Active && t.DeletedAt is null)
            .OrderBy(t => t.Order).ToList());

    public Task<TaskTemplate?> GetByIdAsync(ObjectId id) =>
        Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

    public Task<TaskTemplate> CreateAsync(TaskTemplate template)
    {
        _templates.Add(template);
        return Task.FromResult(template);
    }

    public Task UpdateAsync(TaskTemplate template)
    {
        var index = _templates.FindIndex(t => t.Id == template.Id);
        if (index >= 0) _templates[index] = template;
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(ObjectId id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template is not null)
        {
            template.Active = false;
            template.DeletedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task ActivateAsync(ObjectId id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template is not null)
        {
            template.Active = true;
            template.DeletedAt = null;
            template.UpdatedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }
}

public class FakePointTransactionRepository : IPointTransactionRepository
{
    public readonly List<PointTransaction> Transactions = new();

    public Task<PointTransaction> CreateAsync(PointTransaction transaction)
    {
        Transactions.Add(transaction);
        return Task.FromResult(transaction);
    }

    public Task<int> GetBalanceAsync(ObjectId userId) =>
        Task.FromResult(Transactions.Where(t => t.UserId == userId).Sum(t => t.Points));

    public Task<List<PointTransaction>> GetHistoryAsync(ObjectId userId, int limit = 100) =>
        Task.FromResult(Transactions.Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).Take(limit).ToList());
}

public class FakeTaskEventRepository : ITaskEventRepository
{
    public readonly List<TaskEvent> Events = new();

    public Task<TaskEvent> CreateAsync(TaskEvent taskEvent)
    {
        Events.Add(taskEvent);
        return Task.FromResult(taskEvent);
    }
}

public class FakePacusRepository : IPacusRepository
{
    private readonly List<Pacus.Domain.Entities.Pacus> _pacus = new();

    public Task<Pacus.Domain.Entities.Pacus?> GetByUserIdAsync(ObjectId userId) =>
        Task.FromResult(_pacus.FirstOrDefault(p => p.UserId == userId));

    public Task<Pacus.Domain.Entities.Pacus> CreateAsync(Pacus.Domain.Entities.Pacus pacus)
    {
        _pacus.Add(pacus);
        return Task.FromResult(pacus);
    }

    public Task UpdateAsync(Pacus.Domain.Entities.Pacus pacus)
    {
        var index = _pacus.FindIndex(p => p.Id == pacus.Id);
        if (index >= 0) _pacus[index] = pacus;
        return Task.CompletedTask;
    }
}

public class FakePacusGrowthRepository : IPacusGrowthRepository
{
    public readonly List<PacusGrowthLog> Logs = new();

    public Task<PacusGrowthLog?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        Task.FromResult(Logs.FirstOrDefault(l => l.UserId == userId && l.Date == date));

    public Task<PacusGrowthLog> CreateAsync(PacusGrowthLog log)
    {
        // Simula o indice unico {userId, date} do Mongo.
        if (Logs.Any(l => l.UserId == log.UserId && l.Date == log.Date))
            return Task.FromResult(Logs.First(l => l.UserId == log.UserId && l.Date == log.Date));

        Logs.Add(log);
        return Task.FromResult(log);
    }
}

public class FakeSettingsRepository : ISettingsRepository
{
    private Settings? _settings;

    public FakeSettingsRepository(Settings? seed = null) => _settings = seed;

    public Task<Settings?> GetByUserIdAsync(ObjectId userId) => Task.FromResult(_settings);

    public Task UpsertAsync(Settings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}

public class FakeStoreRepository : IStoreRepository
{
    private readonly List<StoreItem> _items = new();
    private readonly List<Redemption> _redemptions = new();

    public Task<List<StoreItem>> GetActiveItemsAsync(ObjectId userId) =>
        Task.FromResult(_items.Where(i => i.UserId == userId && i.Active).OrderBy(i => i.Cost).ToList());

    public Task<StoreItem?> GetItemByIdAsync(ObjectId id) =>
        Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

    public Task<StoreItem> CreateItemAsync(StoreItem item)
    {
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task UpdateItemAsync(StoreItem item)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index >= 0) _items[index] = item;
        return Task.CompletedTask;
    }

    public Task<Redemption?> GetRedemptionByIdAsync(ObjectId id) =>
        Task.FromResult(_redemptions.FirstOrDefault(r => r.Id == id));

    public Task<Redemption> CreateRedemptionAsync(Redemption redemption)
    {
        _redemptions.Add(redemption);
        return Task.FromResult(redemption);
    }

    public Task UpdateRedemptionAsync(Redemption redemption)
    {
        var index = _redemptions.FindIndex(r => r.Id == redemption.Id);
        if (index >= 0) _redemptions[index] = redemption;
        return Task.CompletedTask;
    }
}
