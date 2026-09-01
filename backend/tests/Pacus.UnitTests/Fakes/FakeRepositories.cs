using MongoDB.Bson;
using Pacus.Application.Exceptions;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.UnitTests.Fakes;

// Fakes em memoria â€” permitem testar os services sem depender de um MongoDB real.

public class FakeDailyRoutineRepository : IDailyRoutineRepository
{
    // Guarda clones (nao a mesma referencia que o service manipula) pra simular o
    // isolamento de um round-trip real com o Mongo -- essencial pra poder testar a
    // concorrencia otimista (achado #5 da auditoria de API de 2026-09-01, ver
    // docs/ESTADO_ATUAL.md e DailyRoutineRepository.UpdateAsync): sem isso, duas
    // "leituras" no teste devolveriam a mesma instancia em memoria e a mutacao de uma
    // apareceria magicamente na outra, o que nunca acontece com o banco de verdade.
    private readonly List<DailyRoutine> _routines = new();

    public Task<DailyRoutine?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        Task.FromResult(
            _routines.FirstOrDefault(
                r => r.FamilyId == userId &&
                     r.Date == date) is { } found
                ? Clone(found)
                : null);

    public Task<DailyRoutine> CreateAsync(
        DailyRoutine routine)
    {
        _routines.Add(Clone(routine));

        return Task.FromResult(routine);
    }

    public Task UpdateAsync(
        DailyRoutine routine)
    {
        var expectedVersion = routine.Version;

        var index =
            _routines.FindIndex(
                r => r.Id == routine.Id && r.Version == expectedVersion);

        if (index < 0)
        {
            throw new ConflictException(
                "Esta rotina foi alterada por outra requisicao enquanto isso era processado. Tente novamente.");
        }

        routine.Version = expectedVersion + 1;
        _routines[index] = Clone(routine);

        return Task.CompletedTask;
    }

    private static DailyRoutine Clone(DailyRoutine source) => new()
    {
        Id = source.Id,
        FamilyId = source.FamilyId,
        Date = source.Date,
        Timezone = source.Timezone,
        Status = source.Status,
        Tasks = source.Tasks.Select(CloneTask).ToList(),
        Stats = new DailyRoutineStats
        {
            Mandatory = new TaskTypeStat { Done = source.Stats.Mandatory.Done, Total = source.Stats.Mandatory.Total },
            Expected = new TaskTypeStat { Done = source.Stats.Expected.Done, Total = source.Stats.Expected.Total },
            Challenge = new TaskTypeStat { Done = source.Stats.Challenge.Done, Total = source.Stats.Challenge.Total },
            PointsEarned = source.Stats.PointsEarned,
            CompletionRate = source.Stats.CompletionRate,
        },
        PointsEarned = source.PointsEarned,
        ClosedAt = source.ClosedAt,
        CreatedAt = source.CreatedAt,
        GameTimerUnlockedAt = source.GameTimerUnlockedAt,
        GameTimerExtraMinutes = source.GameTimerExtraMinutes,
        GameTimerPausedAt = source.GameTimerPausedAt,
        GameTimerPausedMs = source.GameTimerPausedMs,
        Reaction = source.Reaction is null
            ? null
            : new DailyReaction
            {
                Icon = source.Reaction.Icon,
                Message = source.Reaction.Message,
                CreatedBy = source.Reaction.CreatedBy,
                CreatedAt = source.Reaction.CreatedAt,
            },
        GameTimerEnabled = source.GameTimerEnabled,
        GameTimerMinutes = source.GameTimerMinutes,
        Version = source.Version,
    };

    private static DailyTask CloneTask(DailyTask t) => new()
    {
        Id = t.Id,
        TaskTemplateId = t.TaskTemplateId,
        Title = t.Title,
        Description = t.Description,
        Reason = t.Reason,
        Type = t.Type,
        Period = t.Period,
        Order = t.Order,
        Points = t.Points,
        Status = t.Status,
        Options = new List<string>(t.Options),
        SelectedOption = t.SelectedOption,
        CompletedAt = t.CompletedAt,
        CreatedBy = t.CreatedBy,
        Origin = t.Origin,
        DeletedAt = t.DeletedAt,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    public Task<(List<DailyRoutine> Items, long TotalCount)> GetHistoryAsync(
        ObjectId userId,
        string? from,
        string? to,
        int page,
        int pageSize)
    {
        var query =
            _routines.Where(
                r =>
                    r.FamilyId == userId &&
                    r.Status == RoutineStatus.Closed);

        if (from is not null)
        {
            query =
                query.Where(
                    r =>
                        string.Compare(
                            r.Date,
                            from,
                            StringComparison.Ordinal) >= 0);
        }

        if (to is not null)
        {
            query =
                query.Where(
                    r =>
                        string.Compare(
                            r.Date,
                            to,
                            StringComparison.Ordinal) <= 0);
        }

        var ordered =
            query
                .OrderByDescending(
                    r => r.Date)
                .ToList();

        var page_ =
            ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Clone)
                .ToList();

        return Task.FromResult((page_, (long)ordered.Count));
    }

    public Task<DailyRoutine?> GetLatestOpenAsync(
        ObjectId userId) =>
        Task.FromResult(
            _routines
                .Where(
                    r =>
                        r.FamilyId == userId &&
                        r.Status == RoutineStatus.Open)
                .OrderByDescending(
                    r => r.Date)
                .FirstOrDefault() is { } found
                ? Clone(found)
                : null);

    public Task<List<DailyRoutine>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _routines
                .Where(r => r.FamilyId == familyId)
                .OrderByDescending(r => r.Date)
                .Select(Clone)
                .ToList());

    public Task DeleteAllByFamilyAsync(
        ObjectId familyId)
    {
        _routines.RemoveAll(r => r.FamilyId == familyId);

        return Task.CompletedTask;
    }
}

public class FakeTaskTemplateRepository
    : ITaskTemplateRepository
{
    private readonly List<TaskTemplate> _templates;

    public FakeTaskTemplateRepository(
        List<TaskTemplate>? seed = null)
    {
        _templates = seed ?? new();
    }

    public Task<List<TaskTemplate>> GetActiveByUserAsync(
        ObjectId userId) =>
        Task.FromResult(
            _templates
                .Where(
                    t =>
                        t.FamilyId == userId &&
                        t.Active &&
                        t.DeletedAt is null)
                .OrderBy(
                    t => t.Order)
                .ToList());

    public Task<TaskTemplate?> GetByIdAsync(
        ObjectId id) =>
        Task.FromResult(
            _templates.FirstOrDefault(
                t => t.Id == id));

    public Task<TaskTemplate> CreateAsync(
        TaskTemplate template)
    {
        _templates.Add(template);

        return Task.FromResult(template);
    }

    public Task UpdateAsync(
        TaskTemplate template)
    {
        var index =
            _templates.FindIndex(
                t => t.Id == template.Id);

        if (index >= 0)
        {
            _templates[index] = template;
        }

        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(
        ObjectId id)
    {
        var template =
            _templates.FirstOrDefault(
                t => t.Id == id);

        if (template is not null)
        {
            template.Active = false;
            template.DeletedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task ActivateAsync(
        ObjectId id)
    {
        var template =
            _templates.FirstOrDefault(
                t => t.Id == id);

        if (template is not null)
        {
            template.Active = true;
            template.DeletedAt = null;
            template.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<List<TaskTemplate>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _templates
                .Where(t => t.FamilyId == familyId)
                .OrderBy(t => t.Order)
                .ToList());

    public Task DeleteAllByFamilyAsync(
        ObjectId familyId)
    {
        _templates.RemoveAll(t => t.FamilyId == familyId);

        return Task.CompletedTask;
    }
}

public class FakePointTransactionRepository
    : IPointTransactionRepository
{
    public readonly List<PointTransaction> Transactions = new();

    public Task<PointTransaction> CreateAsync(
        PointTransaction transaction)
    {
        Transactions.Add(transaction);

        return Task.FromResult(transaction);
    }

    public Task<int> GetBalanceAsync(
        ObjectId userId) =>
        Task.FromResult(
            Transactions
                .Where(
                    t => t.FamilyId == userId)
                .Sum(
                    t => t.Points));

    public Task<(List<PointTransaction> Items, long TotalCount)> GetHistoryAsync(
        ObjectId userId,
        int page,
        int pageSize)
    {
        var ordered =
            Transactions
                .Where(
                    t => t.FamilyId == userId)
                .OrderByDescending(
                    t => t.CreatedAt)
                .ToList();

        var page_ =
            ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

        return Task.FromResult((page_, (long)ordered.Count));
    }

    public Task<List<PointTransaction>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            Transactions
                .Where(t => t.FamilyId == familyId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList());

    public Task DeleteAllByFamilyAsync(
        ObjectId familyId)
    {
        Transactions.RemoveAll(t => t.FamilyId == familyId);

        return Task.CompletedTask;
    }
}

public class FakeTaskEventRepository
    : ITaskEventRepository
{
    public readonly List<TaskEvent> Events = new();

    public Task<TaskEvent> CreateAsync(
        TaskEvent taskEvent)
    {
        Events.Add(taskEvent);

        return Task.FromResult(taskEvent);
    }

    public Task<List<TaskEvent>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            Events
                .Where(e => e.UserId == familyId)
                .OrderByDescending(e => e.CreatedAt)
                .ToList());

    public Task DeleteAllByFamilyAsync(
        ObjectId familyId)
    {
        Events.RemoveAll(e => e.UserId == familyId);

        return Task.CompletedTask;
    }
}

public class FakePacusRepository
    : IPacusRepository
{
    private readonly List<Pacus.Domain.Entities.Pacus> _pacus =
        new();

    public Task<Pacus.Domain.Entities.Pacus?> GetByFamilyIdAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _pacus.FirstOrDefault(
                p => p.FamilyId == familyId));

    public Task<Pacus.Domain.Entities.Pacus> CreateAsync(
        Pacus.Domain.Entities.Pacus pacus)
    {
        _pacus.Add(pacus);

        return Task.FromResult(pacus);
    }

    public Task UpdateAsync(
        Pacus.Domain.Entities.Pacus pacus)
    {
        var index =
            _pacus.FindIndex(
                p => p.Id == pacus.Id);

        if (index >= 0)
        {
            _pacus[index] = pacus;
        }

        return Task.CompletedTask;
    }

    public Task DeleteByFamilyIdAsync(
        ObjectId familyId)
    {
        _pacus.RemoveAll(p => p.FamilyId == familyId);

        return Task.CompletedTask;
    }
}

public class FakePacusGrowthRepository
    : IPacusGrowthRepository
{
    public readonly List<PacusGrowthLog> Logs = new();

    public Task<PacusGrowthLog?> GetByUserAndDateAsync(
        ObjectId userId,
        string date) =>
        Task.FromResult(
            Logs.FirstOrDefault(
                l =>
                    l.UserId == userId &&
                    l.Date == date));

    public Task<PacusGrowthLog> CreateAsync(
        PacusGrowthLog log)
    {
        // Simula o indice unico {userId, date} do Mongo.
        if (Logs.Any(
            l =>
                l.UserId == log.UserId &&
                l.Date == log.Date))
        {
            return Task.FromResult(
                Logs.First(
                    l =>
                        l.UserId == log.UserId &&
                        l.Date == log.Date));
        }

        Logs.Add(log);

        return Task.FromResult(log);
    }

    public Task<List<PacusGrowthLog>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            Logs
                .Where(l => l.UserId == familyId)
                .OrderByDescending(l => l.Date)
                .ToList());

    public Task DeleteAllByFamilyAsync(
        ObjectId familyId)
    {
        Logs.RemoveAll(l => l.UserId == familyId);

        return Task.CompletedTask;
    }
}

public class FakeSettingsRepository
    : ISettingsRepository
{
    private Settings? _settings;

    public FakeSettingsRepository(
        Settings? seed = null)
    {
        _settings = seed;
    }

    public Task<Settings?> GetByUserIdAsync(
        ObjectId userId) =>
        Task.FromResult(_settings);

    public Task UpsertAsync(
        Settings settings)
    {
        _settings = settings;

        return Task.CompletedTask;
    }

    public Task DeleteByFamilyIdAsync(
        ObjectId familyId)
    {
        if (_settings is not null && _settings.FamilyId == familyId)
        {
            _settings = null;
        }

        return Task.CompletedTask;
    }
}

public class FakeStoreRepository
    : IStoreRepository
{
    private readonly List<StoreItem> _items = new();
    private readonly List<Redemption> _redemptions = new();

    public Task<List<StoreItem>> GetActiveItemsAsync(
        ObjectId userId) =>
        Task.FromResult(
            _items
                .Where(
                    i =>
                        i.FamilyId == userId &&
                        i.Active)
                .OrderBy(
                    i => i.Cost)
                .ToList());

    public Task<StoreItem?> GetItemByIdAsync(
        ObjectId id) =>
        Task.FromResult(
            _items.FirstOrDefault(
                i => i.Id == id));

    public Task<StoreItem> CreateItemAsync(
        StoreItem item)
    {
        _items.Add(item);

        return Task.FromResult(item);
    }

    public Task UpdateItemAsync(
        StoreItem item)
    {
        var index =
            _items.FindIndex(
                i => i.Id == item.Id);

        if (index >= 0)
        {
            _items[index] = item;
        }

        return Task.CompletedTask;
    }

    public Task<Redemption?> GetRedemptionByIdAsync(
        ObjectId id) =>
        Task.FromResult(
            _redemptions.FirstOrDefault(
                r => r.Id == id));

    public Task<Redemption> CreateRedemptionAsync(
        Redemption redemption)
    {
        _redemptions.Add(redemption);

        return Task.FromResult(redemption);
    }

    public Task UpdateRedemptionAsync(
        Redemption redemption)
    {
        var index =
            _redemptions.FindIndex(
                r => r.Id == redemption.Id);

        if (index >= 0)
        {
            _redemptions[index] = redemption;
        }

        return Task.CompletedTask;
    }

    public Task<List<Redemption>> GetRedemptionsByItemSinceAsync(
        ObjectId familyId, ObjectId storeItemId, DateTime sinceUtc) =>
        Task.FromResult(
            _redemptions
                .Where(
                    r =>
                        r.FamilyId == familyId &&
                        r.StoreItemId == storeItemId &&
                        r.RequestedAt >= sinceUtc)
                .ToList());

    public Task<List<Redemption>> GetPendingRedemptionsByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _redemptions
                .Where(
                    r =>
                        r.FamilyId == familyId &&
                        r.Status == RedemptionStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToList());

    public Task<List<StoreItem>> GetAllItemsByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _items
                .Where(i => i.FamilyId == familyId)
                .OrderBy(i => i.Cost)
                .ToList());

    public Task<List<Redemption>> GetAllRedemptionsByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            _redemptions
                .Where(r => r.FamilyId == familyId)
                .OrderByDescending(r => r.RequestedAt)
                .ToList());

    public Task DeleteAllItemsByFamilyAsync(
        ObjectId familyId)
    {
        _items.RemoveAll(i => i.FamilyId == familyId);

        return Task.CompletedTask;
    }

    public Task DeleteAllRedemptionsByFamilyAsync(
        ObjectId familyId)
    {
        _redemptions.RemoveAll(r => r.FamilyId == familyId);

        return Task.CompletedTask;
    }
}

public class FakeAuditLogRepository
    : IAuditLogRepository
{
    public readonly List<AuditLog> Logs = new();

    public Task<AuditLog> CreateAsync(
        AuditLog log)
    {
        Logs.Add(log);

        return Task.FromResult(log);
    }

    public Task<List<AuditLog>> GetAllByFamilyAsync(
        ObjectId familyId) =>
        Task.FromResult(
            Logs
                .Where(l => l.FamilyId == familyId)
                .OrderByDescending(l => l.CreatedAt)
                .ToList());

    public Task AnonymizeByFamilyAsync(
        ObjectId familyId,
        DateTime purgeAt)
    {
        foreach (var log in Logs.Where(l => l.FamilyId == familyId))
        {
            log.ActorId = ObjectId.Empty;
            log.Anonymized = true;
            log.PurgeAt = purgeAt;
        }

        return Task.CompletedTask;
    }
}
