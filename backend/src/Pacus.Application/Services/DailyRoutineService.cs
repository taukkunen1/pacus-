using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class DailyRoutineService : IDailyRoutineService
{
    private readonly IDailyRoutineRepository _dailyRoutineRepository;
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly ITaskEventRepository _taskEventRepository;
    private readonly IPointsService _pointsService;
    private readonly ISettingsRepository _settingsRepository;

    public DailyRoutineService(
        IDailyRoutineRepository dailyRoutineRepository,
        ITaskTemplateRepository taskTemplateRepository,
        ITaskEventRepository taskEventRepository,
        IPointsService pointsService,
        ISettingsRepository settingsRepository)
    {
        _dailyRoutineRepository = dailyRoutineRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _taskEventRepository = taskEventRepository;
        _pointsService = pointsService;
        _settingsRepository = settingsRepository;
    }

    public async Task<DailyRoutine> GetOrCreateTodayAsync(ObjectId userId, string timezone)
    {
        var today = TimezoneHelper.GetOperationalDate(timezone);

        var existing =
            await _dailyRoutineRepository.GetByUserAndDateAsync(userId, today);

        DailyRoutine routine;
        if (existing is null)
        {
            routine = await CreateRoutineForDateAsync(userId, today, timezone);
        }
        else
        {
            if (existing.Status == RoutineStatus.Open)
                await SyncMissingTemplatesAsync(existing, userId);
            routine = existing;
        }

        await SyncGameTimerAsync(routine, userId);
        return routine;
    }

    private async Task SyncMissingTemplatesAsync(
        DailyRoutine routine,
        ObjectId userId)
    {
        var templates =
            await _taskTemplateRepository.GetActiveByUserAsync(userId);

        // Inclui tarefas ja deletadas (DeletedAt != null) para nao recriar
        // uma tarefa permanente que o usuario removeu apenas para hoje.
        var existingTemplateIds = routine.Tasks
            .Where(t => t.TaskTemplateId is not null)
            .Select(t => t.TaskTemplateId!)
            .ToHashSet();

        var missingTemplates = templates
            .Where(t => !existingTemplateIds.Contains(t.Id.ToString()))
            .OrderBy(t => t.Order)
            .ToList();

        if (missingTemplates.Count == 0)
            return;

        var nextOrder = routine.Tasks
            .Where(t => t.DeletedAt is null)
            .Select(t => t.Order)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var dayOfWeek = ParseDayOfWeek(routine.Date);

        foreach (var template in missingTemplates)
        {
            var resolved = ResolveTemplateForDay(template, dayOfWeek);
            if (resolved is null) continue; // recorrencia nao inclui este dia (ex.: fim de semana)

            routine.Tasks.Add(new DailyTask
            {
                Id = Guid.NewGuid().ToString(),
                TaskTemplateId = template.Id.ToString(),
                Title = resolved.Title,
                Description = resolved.Description,
                Type = template.Type,
                Period = template.Period,
                Order = nextOrder++,
                Points = template.Points,
                Status = TaskItemStatus.Pending,
                CompletedAt = null,
                CreatedBy = userId.ToString(),
                Origin = "template",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        routine.Tasks = routine.Tasks
            .OrderBy(t => t.Order)
            .ToList();

        routine.Stats = BuildStats(routine.Tasks);

        routine.PointsEarned = routine.Tasks
            .Where(t =>
                t.Status == TaskItemStatus.Done &&
                t.DeletedAt is null)
            .Sum(t => t.Points);

        await _dailyRoutineRepository.UpdateAsync(routine);
    }

    public async Task<DailyRoutine> CreateRoutineForDateAsync(ObjectId userId, string date, string timezone)
    {
        var existing = await _dailyRoutineRepository.GetByUserAndDateAsync(userId, date);
        if (existing is not null) return existing;

        var templates = await _taskTemplateRepository.GetActiveByUserAsync(userId);
        var dayOfWeek = ParseDayOfWeek(date);

        var tasks = templates
            .Select(t => (Template: t, Resolved: ResolveTemplateForDay(t, dayOfWeek)))
            .Where(pair => pair.Resolved is not null) // recorrencia nao inclui este dia
            .Select(pair => new DailyTask
            {
                Id = Guid.NewGuid().ToString(),
                TaskTemplateId = pair.Template.Id.ToString(),
                Title = pair.Resolved!.Title,
                Description = pair.Resolved!.Description,
                Type = pair.Template.Type,
                Period = pair.Template.Period,
                Order = pair.Template.Order,
                Points = pair.Template.Points,
                Status = TaskItemStatus.Pending,
                CompletedAt = null,
                CreatedBy = userId.ToString(),
                Origin = "template",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }).ToList();

        var routine = new DailyRoutine
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = userId,
            Date = date,
            Timezone = timezone,
            Status = RoutineStatus.Open,
            Tasks = tasks,
            Stats = BuildStats(tasks),
            PointsEarned = 0,
            ClosedAt = null,
            CreatedAt = DateTime.UtcNow,
        };

        return await _dailyRoutineRepository.CreateAsync(routine);
    }

    public async Task<DailyRoutine> ToggleTaskAsync(
        ObjectId userId, string taskId, bool completed, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId)
            ?? throw new InvalidOperationException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var wasCompleted = task.Status == TaskItemStatus.Done;
        if (wasCompleted == completed)
            return routine;

        task.Status = completed ? TaskItemStatus.Done : TaskItemStatus.Pending;
        task.CompletedAt = completed ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;

        routine.Stats = BuildStats(routine.Tasks);
        routine.PointsEarned = routine.Tasks
            .Where(t => t.Status == TaskItemStatus.Done && t.DeletedAt is null)
            .Sum(t => t.Points);

        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        var actorRoleEnum = actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Adult
            : UserRole.Child;

        await _pointsService.RecordAsync(
            userId,
            routine.Id,
            routine.Date,
            task.Id,
            task.Title,
            completed ? PointTransactionType.Award : PointTransactionType.Reversal,
            completed ? task.Points : -task.Points,
            actorId,
            actorRoleEnum);

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = task.TaskTemplateId is null ? null : ObjectId.Parse(task.TaskTemplateId),
            EventType = completed ? TaskEventType.Completed : TaskEventType.Reopened,
            ActorId = actorId,
            ActorRole = actorRoleEnum,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    public async Task<DailyRoutine> CreateAdHocTaskAsync(
        ObjectId userId, CreateTaskRequest request, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        if (!Enum.TryParse<TaskType>(request.Type, ignoreCase: true, out var type))
            throw new InvalidOperationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, ignoreCase: true, out var period))
            throw new InvalidOperationException($"Periodo invalido: {request.Period}");
        ValidatePoints(request.Points);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("O titulo da tarefa e obrigatorio.");
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanCreateTasks);

        var actorRoleEnum = actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Adult
            : UserRole.Child;

        var template = new TaskTemplate
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = userId,
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Period = period,
            Points = request.Points,
            Order = routine.Tasks.Count + 1,
            Active = false,
            Recurrence = "daily",
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await _taskTemplateRepository.CreateAsync(template);

        var task = new DailyTask
        {
            Id = Guid.NewGuid().ToString(),
            TaskTemplateId = template.Id.ToString(),
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Period = period,
            Order = routine.Tasks.Count + 1,
            Points = request.Points,
            Status = TaskItemStatus.Pending,
            CompletedAt = null,
            CreatedBy = actorId.ToString(),
            Origin = actorRole.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        routine.Tasks.Add(task);
        routine.Stats = BuildStats(routine.Tasks);
        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = template.Id,
            EventType = TaskEventType.Created,
            ActorId = actorId,
            ActorRole = actorRoleEnum,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    public async Task<DailyRoutine> ReorderTasksAsync(
        ObjectId userId, List<string> orderedTaskIds, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanReorderTasks);

        var currentIds = routine.Tasks.Where(t => t.DeletedAt is null).Select(t => t.Id).ToHashSet();
        var requestedIds = orderedTaskIds.ToHashSet();
        if (!currentIds.SetEquals(requestedIds))
        {
            throw new InvalidOperationException(
                "A lista de ordenacao precisa conter exatamente as tarefas da rotina de hoje.");
        }

        for (var i = 0; i < orderedTaskIds.Count; i++)
        {
            var task = routine.Tasks.First(t => t.Id == orderedTaskIds[i]);
            task.Order = i + 1;
            task.UpdatedAt = DateTime.UtcNow;
        }
        routine.Tasks = routine.Tasks.OrderBy(t => t.Order).ToList();

        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        var actorRoleEnum = actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Adult
            : UserRole.Child;

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            EventType = TaskEventType.Reordered,
            ActorId = actorId,
            ActorRole = actorRoleEnum,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    public async Task<DailyRoutine> AdjustTaskPointsAsync(
        ObjectId userId, string taskId, int newPoints, ObjectId actorId, string actorRole)
    {
        ValidatePoints(newPoints);

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanSetPoints);

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new InvalidOperationException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var oldPoints = task.Points;
        if (oldPoints == newPoints) return routine;

        var wasDone = task.Status == TaskItemStatus.Done;
        task.Points = newPoints;
        task.UpdatedAt = DateTime.UtcNow;

        routine.Stats = BuildStats(routine.Tasks);
        routine.PointsEarned = routine.Tasks
            .Where(t => t.Status == TaskItemStatus.Done && t.DeletedAt is null)
            .Sum(t => t.Points);

        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        var actorRoleEnum = actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Adult
            : UserRole.Child;

        if (wasDone)
        {
            var delta = newPoints - oldPoints;
            await _pointsService.RecordAsync(
                userId,
                routine.Id,
                routine.Date,
                task.Id,
                task.Title,
                PointTransactionType.Adjustment,
                delta,
                actorId,
                actorRoleEnum,
                reason: $"Ajuste de pontos: {task.Title} ({oldPoints} -> {newPoints})");
        }

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = task.TaskTemplateId is null ? null : TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.PointsAdjusted,
            ActorId = actorId,
            ActorRole = actorRoleEnum,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    public async Task<DailyRoutine> UpdateTaskAsync(
        ObjectId userId, string taskId, DailyTaskUpdateRequest request, ObjectId actorId, string actorRole)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("O titulo da tarefa e obrigatorio.");
        if (!Enum.TryParse<TaskType>(request.Type, true, out var type))
            throw new InvalidOperationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, true, out var period))
            throw new InvalidOperationException($"Periodo invalido: {request.Period}");
        ValidatePoints(request.Points);
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanEditTasks);

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new InvalidOperationException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var oldPoints = task.Points;
        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Type = type;
        task.Period = period;
        task.Points = request.Points;
        task.UpdatedAt = DateTime.UtcNow;
        routine.Stats = BuildStats(routine.Tasks);
        routine.PointsEarned = routine.Tasks.Where(t => t.Status == TaskItemStatus.Done && t.DeletedAt is null).Sum(t => t.Points);
        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        var role = ParseRole(actorRole);
        if (task.Status == TaskItemStatus.Done && oldPoints != request.Points)
        {
            await _pointsService.RecordAsync(userId, routine.Id, routine.Date, task.Id, task.Title,
                PointTransactionType.Adjustment, request.Points - oldPoints, actorId, role,
                $"Ajuste de pontos: {task.Title} ({oldPoints} -> {request.Points})");
        }

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(), UserId = userId, DailyRoutineId = routine.Id,
            TaskId = task.Id, TaskTemplateId = TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.Updated, ActorId = actorId, ActorRole = role, CreatedAt = DateTime.UtcNow
        });
        return routine;
    }

    public async Task<DailyRoutine> DeleteTaskAsync(
        ObjectId userId, string taskId, ObjectId actorId, string actorRole)
    {
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanDeleteTasks);
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new InvalidOperationException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var wasDone = task.Status == TaskItemStatus.Done;
        task.DeletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        routine.Stats = BuildStats(routine.Tasks);
        routine.PointsEarned = routine.Tasks.Where(t => t.Status == TaskItemStatus.Done && t.DeletedAt is null).Sum(t => t.Points);
        await SyncGameTimerAsync(routine, userId);
        await _dailyRoutineRepository.UpdateAsync(routine);

        var role = ParseRole(actorRole);
        if (wasDone)
        {
            await _pointsService.RecordAsync(userId, routine.Id, routine.Date, task.Id, task.Title,
                PointTransactionType.Reversal, -task.Points, actorId, role,
                $"Tarefa removida: {task.Title}");
        }

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(), UserId = userId, DailyRoutineId = routine.Id,
            TaskId = task.Id, TaskTemplateId = TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.Deleted, ActorId = actorId, ActorRole = role, CreatedAt = DateTime.UtcNow
        });
        return routine;
    }

    private async Task EnsureChildPermissionAsync(
        ObjectId userId,
        string actorRole,
        Func<ChildPermissions, bool> permission)
    {
        if (!actorRole.Equals("child", StringComparison.OrdinalIgnoreCase))
            return;

        var settings = await _settingsRepository.GetByUserIdAsync(userId);
        if (settings is not null && !permission(settings.ChildPermissions))
            throw new UnauthorizedAccessException(
                "Esta acao nao esta permitida no painel infantil.");
    }

    public async Task<DailyRoutine> PauseGameTimerAsync(ObjectId userId, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        if (routine.GameTimerUnlockedAt is null || routine.GameTimerPausedAt is not null)
            return routine; // nada pra pausar, ou ja esta pausado

        routine.GameTimerPausedAt = DateTime.UtcNow;
        await SyncGameTimerAsync(routine, userId); // repopula GameTimerEnabled/Minutes (BsonIgnore, nao persistidos)
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> ResumeGameTimerAsync(ObjectId userId, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        if (routine.GameTimerPausedAt is null)
            return routine; // ja esta rodando

        routine.GameTimerPausedMs += (long)(DateTime.UtcNow - routine.GameTimerPausedAt.Value).TotalMilliseconds;
        routine.GameTimerPausedAt = null;
        await SyncGameTimerAsync(routine, userId); // repopula GameTimerEnabled/Minutes (BsonIgnore, nao persistidos)
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> AdjustGameTimerAsync(ObjectId userId, int deltaMinutes, ObjectId actorId, string actorRole)
    {
        if (!actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Ajustar o tempo do game timer e restrito ao painel adulto.");

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new InvalidOperationException("Nenhuma rotina em aberto para este usuario.");

        await SyncGameTimerAsync(routine, userId); // garante GameTimerMinutes atualizado antes do clamp

        var proposedExtra = routine.GameTimerExtraMinutes + deltaMinutes;
        var totalMinutes = routine.GameTimerMinutes + proposedExtra;
        // nunca deixa o total ficar negativo (so trava em 0, nao impede reduzir o resto)
        routine.GameTimerExtraMinutes = totalMinutes < 0
            ? -routine.GameTimerMinutes
            : proposedExtra;

        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    private async Task SyncGameTimerAsync(DailyRoutine routine, ObjectId userId)
    {
        var settings = await _settingsRepository.GetByUserIdAsync(userId);
        routine.GameTimerEnabled = settings?.GameTimerEnabled ?? false;
        routine.GameTimerMinutes = settings?.GameTimerMinutes ?? 120;

        if (routine.GameTimerUnlockedAt is not null || !routine.GameTimerEnabled)
            return;

        var morningTasks = routine.Tasks
            .Where(t => t.DeletedAt is null && t.Period == TaskPeriod.Morning)
            .ToList();

        if (morningTasks.Count > 0 && morningTasks.All(t => t.Status == TaskItemStatus.Done))
            routine.GameTimerUnlockedAt = DateTime.UtcNow;
    }

    private static UserRole ParseRole(string actorRole) =>
        actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Adult
            : UserRole.Child;

    private static ObjectId? TryParseObjectId(string? value) =>
        ObjectId.TryParse(value, out var id) ? id : null;

    // Data operacional "YYYY-MM-DD" -> dia da semana. DateTime.ParseExact e suficiente
    // aqui (nao precisa de timezone: a data ja veio resolvida no timezone da familia
    // por TimezoneHelper.GetOperationalDate antes de chegar em qualquer chamador).
    private static DayOfWeek ParseDayOfWeek(string date) =>
        DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).DayOfWeek;

    // Decide se/como um TaskTemplate aparece num dia especifico, de acordo com
    // Recurrence. Retorna null quando a recorrencia nao inclui esse dia da semana
    // (o chamador deve pular esse template pra essa data). Titulo/descricao no
    // retorno ja vem resolvidos (iguais ao template, exceto em
    // RecurrenceWeekdayRotation, onde vem da variante do dia).
    private static ResolvedTemplateContent? ResolveTemplateForDay(TaskTemplate template, DayOfWeek dayOfWeek)
    {
        var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekday, StringComparison.OrdinalIgnoreCase))
        {
            return isWeekend ? null : new ResolvedTemplateContent(template.Title, template.Description);
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekend, StringComparison.OrdinalIgnoreCase))
        {
            return isWeekend ? new ResolvedTemplateContent(template.Title, template.Description) : null;
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekdayRotation, StringComparison.OrdinalIgnoreCase))
        {
            var variant = template.Variants.FirstOrDefault(v => v.DayOfWeek == dayOfWeek);
            return variant is null ? null : new ResolvedTemplateContent(variant.Title, variant.Description);
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceCustom, StringComparison.OrdinalIgnoreCase))
        {
            // Mesmo conteudo do template todo dia escolhido -- so a lista de dias
            // muda (ex.: "Ingles" so terca e quarta, "Escoteiro" so sabado).
            return template.CustomDays.Contains(dayOfWeek)
                ? new ResolvedTemplateContent(template.Title, template.Description)
                : null;
        }

        // RecurrenceDaily (ou qualquer valor desconhecido/legado): comportamento
        // original, todo dia, com o conteudo do proprio template.
        return new ResolvedTemplateContent(template.Title, template.Description);
    }

    private sealed record ResolvedTemplateContent(string Title, string? Description);

    private static void ValidatePoints(int points)
    {
        if (points == 0 || points < -10 || points > 10)
            throw new InvalidOperationException(
                "Cada tarefa deve valer entre 1 e 10 Pacus Points, ou entre -1 e -10 (penalidade). Zero nao e permitido.");
    }

    private static DailyRoutineStats BuildStats(List<DailyTask> tasks)
    {
        var active = tasks.Where(t => t.DeletedAt is null).ToList();

        TaskTypeStat StatFor(TaskType type)
        {
            var ofType = active.Where(t => t.Type == type).ToList();

            return new TaskTypeStat
            {
                Total = ofType.Count,
                Done = ofType.Count(t => t.Status == TaskItemStatus.Done),
            };
        }

        var mandatory = StatFor(TaskType.Mandatory);
        var expected = StatFor(TaskType.Expected);
        var challenge = StatFor(TaskType.Challenge);

        var totalTasks = active.Count;
        var totalDone = active.Count(t => t.Status == TaskItemStatus.Done);

        var pointsEarned = active
            .Where(t => t.Status == TaskItemStatus.Done)
            .Sum(t => t.Points);

        return new DailyRoutineStats
        {
            Mandatory = mandatory,
            Expected = expected,
            Challenge = challenge,
            PointsEarned = pointsEarned,
            CompletionRate =
                totalTasks == 0
                    ? 0
                    : Math.Round((double)totalDone / totalTasks, 2),
        };
    }
}
