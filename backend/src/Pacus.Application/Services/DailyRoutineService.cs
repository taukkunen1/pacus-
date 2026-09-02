using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Application.Exceptions;

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

        foreach (var template in missingTemplates)
        {
            var resolved = ResolveTemplateForDay(template, routine.Date);
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
                Points = resolved.Points,
                Status = TaskItemStatus.Pending,
                Options = new List<string>(template.Options),
                Reason = PickReason(template.EffectiveReasons),
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

        var tasks = templates
            .Select(t => (Template: t, Resolved: ResolveTemplateForDay(t, date)))
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
                Points = pair.Resolved!.Points,
                Status = TaskItemStatus.Pending,
                Options = new List<string>(pair.Template.Options),
                Reason = PickReason(pair.Template.EffectiveReasons),
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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

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

    // Crianca (ou adulto) escolhe qual das Options da tarefa vai seguir -- pensado pra
    // ser chamado antes de concluir, mas nao trava a conclusao se pular (Options
    // continua so uma sugestao de caminho, nunca um bloqueio). SelectedOption nulo
    // limpa a escolha (ex.: a crianca mudou de ideia antes de concluir).
    public async Task<DailyRoutine> SelectTaskOptionAsync(
        ObjectId userId, string taskId, string? selectedOption, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

        if (selectedOption is not null && !task.Options.Contains(selectedOption))
            throw new ValidationException("Essa opcao nao existe para esta tarefa.");

        task.SelectedOption = selectedOption;
        task.UpdatedAt = DateTime.UtcNow;
        await _dailyRoutineRepository.UpdateAsync(routine);

        var role = ParseRole(actorRole);
        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.OptionSelected,
            ActorId = actorId,
            ActorRole = role,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    public async Task<DailyRoutine> CreateAdHocTaskAsync(
        ObjectId userId, CreateTaskRequest request, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        if (!Enum.TryParse<TaskType>(request.Type, ignoreCase: true, out var type))
            throw new ValidationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, ignoreCase: true, out var period))
            throw new ValidationException($"Periodo invalido: {request.Period}");
        ValidatePoints(request.Points);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("O titulo da tarefa e obrigatorio.");
        var options = TaskTemplateService.ParseOptions(request.Options);
        var reason = TaskTemplateService.ParseSingleReason(request.Reason);
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
            Options = options,
            Reasons = reason is null ? new List<string>() : new List<string> { reason },
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
            Options = options,
            Reason = reason,
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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanReorderTasks);

        var currentIds = routine.Tasks.Where(t => t.DeletedAt is null).Select(t => t.Id).ToHashSet();
        var requestedIds = orderedTaskIds.ToHashSet();
        if (!currentIds.SetEquals(requestedIds))
        {
            throw new ValidationException(
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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanSetPoints);

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

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
            throw new ValidationException("O titulo da tarefa e obrigatorio.");
        if (!Enum.TryParse<TaskType>(request.Type, true, out var type))
            throw new ValidationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, true, out var period))
            throw new ValidationException($"Periodo invalido: {request.Period}");
        ValidatePoints(request.Points);
        var options = TaskTemplateService.ParseOptions(request.Options);
        var reason = TaskTemplateService.ParseSingleReason(request.Reason);
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanEditTasks);

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var oldPoints = task.Points;
        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Type = type;
        task.Period = period;
        task.Points = request.Points;
        task.Options = options;
        task.Reason = reason;
        // Se a opcao escolhida antes nao existe mais na lista nova, descarta -- nao faz
        // sentido manter uma "escolha" que nao e mais uma opcao valida da tarefa.
        if (task.SelectedOption is not null && !options.Contains(task.SelectedOption))
            task.SelectedOption = null;
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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

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
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

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

    // Chaves semanticas dos icones disponiveis pra reacao (ver DailyReaction.Icon) —
    // frontend mapeia cada uma pro emoji + frase padrao (ver pacus/habitat.js).
    public static readonly HashSet<string> AllowedReactionIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        "heart", "clap", "star", "hug"
    };

    // Vinculo (relatedness -- ver docs/PROPOSITO.md e DailyReaction). Restrito a adulto;
    // um por dia -- reagir de novo no mesmo dia substitui a reacao anterior (nao acumula,
    // granularidade "por dia" escolhida pelo dono do produto). Nao trava nada, nao gera
    // pontos -- e so vinculo, sem virar mais um mecanismo de recompensa.
    public async Task<DailyRoutine> SetReactionAsync(
        ObjectId userId, string icon, string? message, ObjectId actorId, string actorRole)
    {
        if (!actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Reagir ao dia e restrito ao painel adulto.");

        icon ??= string.Empty;
        if (!AllowedReactionIcons.Contains(icon))
            throw new ValidationException(
                $"Icone de reacao invalido: {icon}. Use um destes: {string.Join(", ", AllowedReactionIcons)}.");

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        routine.Reaction = new DailyReaction
        {
            Icon = icon.ToLowerInvariant(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
        };

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

    // Sorteia uma frase do pool de motivos do template pra este DailyTask (pedido do
    // dono do produto, 2026-09-02: "frases aleatorias e motivos sempre pertinentes,
    // nao precisa ser a mesma frase todos os dias"). So roda no momento em que o
    // DailyTask e gerado (aqui e em CreateRoutineForDateAsync) -- depois disso o
    // DailyTask e imutavel como qualquer outro campo copiado do template, entao a
    // frase sorteada fica fixa para aquele dia especifico (nao muda se a pessoa
    // recarregar a tela), mas o proximo dia gerado sorteia de novo. Random.Shared
    // (thread-safe, .NET 6+) em vez de `new Random()` porque varias rotinas de
    // familias diferentes podem ser geradas concorrentemente.
    private static string? PickReason(List<string> reasons) =>
        reasons.Count == 0 ? null : reasons[Random.Shared.Next(reasons.Count)];

    // Decide se/como um TaskTemplate aparece num dia especifico, de acordo com
    // Recurrence. Retorna null quando a recorrencia nao inclui esse dia (o chamador
    // deve pular esse template pra essa data). Titulo/descricao/pontos no retorno ja
    // vem resolvidos (iguais ao template, exceto em RecurrenceWeekdayRotation, onde
    // titulo/descricao vem da variante do dia, e os pontos tambem vem da variante
    // quando ela define um valor proprio -- ex.: uma missao que exige supervisao de
    // adulto pode valer mais que outra). Recebe a data operacional inteira (nao so o
    // dia da semana) porque RecurrenceInterval precisa contar dias corridos desde uma
    // data-ancora -- um "dia sim, dia nao" desliza pelos dias da semana com o tempo,
    // diferente de RecurrenceCustom, que e sempre os mesmos dias toda semana.
    private static ResolvedTemplateContent? ResolveTemplateForDay(TaskTemplate template, string date)
    {
        var dayOfWeek = ParseDayOfWeek(date);
        var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekday, StringComparison.OrdinalIgnoreCase))
        {
            return isWeekend ? null : new ResolvedTemplateContent(template.Title, template.Description, template.Points);
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekend, StringComparison.OrdinalIgnoreCase))
        {
            return isWeekend ? new ResolvedTemplateContent(template.Title, template.Description, template.Points) : null;
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceWeekdayRotation, StringComparison.OrdinalIgnoreCase))
        {
            var variant = template.Variants.FirstOrDefault(v => v.DayOfWeek == dayOfWeek);
            return variant is null
                ? null
                : new ResolvedTemplateContent(variant.Title, variant.Description, variant.Points ?? template.Points);
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceCustom, StringComparison.OrdinalIgnoreCase))
        {
            // Mesmo conteudo do template todo dia escolhido -- so a lista de dias
            // muda (ex.: "Ingles" so terca e quarta, "Escoteiro" so sabado).
            return template.CustomDays.Contains(dayOfWeek)
                ? new ResolvedTemplateContent(template.Title, template.Description, template.Points)
                : null;
        }

        if (template.Recurrence.Equals(TaskTemplate.RecurrenceInterval, StringComparison.OrdinalIgnoreCase))
        {
            return IsIntervalDay(template, date)
                ? new ResolvedTemplateContent(template.Title, template.Description, template.Points)
                : null;
        }

        // RecurrenceDaily (ou qualquer valor desconhecido/legado): comportamento
        // original, todo dia, com o conteudo do proprio template.
        return new ResolvedTemplateContent(template.Title, template.Description, template.Points);
    }

    // "Dia sim, dia nao" (IntervalDays == 2) ou qualquer intervalo de N dias, contado
    // em dias corridos desde AnchorDate (nao dias uteis, nao "toda outra semana" --
    // simplesmente (data - ancora) % N == 0). Datas antes da ancora nunca incluem a
    // tarefa. Template mal configurado (sem AnchorDate, por algum dado legado ou
    // corrompido) tambem nunca inclui, em vez de lancar excecao no meio da geracao da
    // rotina inteira -- mais seguro falhar "silencioso" pra uma tarefa do que quebrar
    // o carregamento do dia todo.
    private static bool IsIntervalDay(TaskTemplate template, string date)
    {
        if (string.IsNullOrWhiteSpace(template.AnchorDate))
            return false;

        if (!DateTime.TryParseExact(
                template.AnchorDate,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var anchor))
        {
            return false;
        }

        var today = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var daysSinceAnchor = (today - anchor).Days;
        var interval = template.IntervalDays < 1 ? 2 : template.IntervalDays;

        return daysSinceAnchor >= 0 && daysSinceAnchor % interval == 0;
    }

    private sealed record ResolvedTemplateContent(string Title, string? Description, int Points);

    private static void ValidatePoints(int points)
    {
        if (points == 0 || points < -10 || points > 10)
            throw new ValidationException(
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
