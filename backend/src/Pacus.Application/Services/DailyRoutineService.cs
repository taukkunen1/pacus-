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
            if (existing.Status == RoutineStatus.Planned)
            {
                existing.Status = RoutineStatus.Open;
                await _dailyRoutineRepository.UpdateAsync(existing);
            }

            if (existing.Status == RoutineStatus.Open)
                await SyncMissingTemplatesAsync(existing, userId);

            routine = existing;
        }

        await SyncGameTimerAsync(routine, userId);
        return routine;
    }

    public async Task<DailyRoutine> GetOrCreateTomorrowAsync(ObjectId userId, string timezone)
    {
        var today = TimezoneHelper.GetOperationalDate(timezone);
        var tomorrow = TimezoneHelper.NextDate(today);
        var existing = await _dailyRoutineRepository.GetByUserAndDateAsync(userId, tomorrow);

        if (existing is null)
            return await CreateRoutineForDateAsync(userId, tomorrow, timezone, RoutineStatus.Planned);

        if (existing.Status == RoutineStatus.Planned)
            await SyncMissingTemplatesAsync(existing, userId);

        return existing;
    }

    public async Task<DailyRoutine> CreateTomorrowTaskAsync(
        ObjectId userId,
        TomorrowTaskRequest request,
        ObjectId actorId,
        string actorRole,
        string timezone)
    {
        TaskValidation.ValidateTitle(request.Title);
        TaskValidation.ValidateDescription(request.Description);
        if (!Enum.TryParse<TaskPeriod>(request.Period, true, out var period))
            throw new ValidationException($"Periodo invalido: {request.Period}");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanCreateTasks);
        var routine = await GetOrCreateTomorrowAsync(userId, timezone);

        var nextOrder = routine.Tasks
            .Where(t => t.DeletedAt is null)
            .Select(t => t.Order)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var isMember = actorRole.Equals("child", StringComparison.OrdinalIgnoreCase);
        routine.Tasks.Add(new DailyTask
        {
            Id = Guid.NewGuid().ToString(),
            TaskTemplateId = null,
            Title = request.Title.Trim(),
            Description = request.Description,
            Type = TaskType.Expected,
            Period = period,
            Order = nextOrder,
            Points = 0,
            Status = TaskItemStatus.Pending,
            CreatedBy = actorId.ToString(),
            Origin = isMember ? "child" : "adult",
            PlannedBy = isMember ? "member" : "adult",
            CreatedByMember = isMember,
            PlanCue = string.IsNullOrWhiteSpace(request.PlanCue) ? null : request.PlanCue.Trim(),
            RequiresAdultApproval = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        routine.Stats = BuildStats(routine.Tasks);
        routine.TomorrowPlanConfirmedAt = null;
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> UpdateTomorrowTaskAsync(
        ObjectId userId,
        string taskId,
        UpdateTomorrowTaskRequest request,
        ObjectId actorId,
        string actorRole,
        string timezone)
    {
        TaskValidation.ValidateTitle(request.Title);
        TaskValidation.ValidateDescription(request.Description);
        if (!Enum.TryParse<TaskPeriod>(request.Period, true, out var period))
            throw new ValidationException($"Periodo invalido: {request.Period}");

        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanEditTasks);
        var routine = await GetOrCreateTomorrowAsync(userId, timezone);
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada no plano de amanha.");

        var isMember = actorRole.Equals("child", StringComparison.OrdinalIgnoreCase);
        if (isMember && !task.CreatedByMember)
            throw new UnauthorizedAccessException("Tarefas combinadas podem ser reorganizadas, mas precisam de um adulto para serem alteradas.");

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Period = period;
        task.PlanCue = string.IsNullOrWhiteSpace(request.PlanCue) ? null : request.PlanCue.Trim();
        task.PlannedBy = isMember ? "member" : "adult";
        task.UpdatedAt = DateTime.UtcNow;

        routine.Stats = BuildStats(routine.Tasks);
        routine.TomorrowPlanConfirmedAt = null;
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> DeleteTomorrowTaskAsync(
        ObjectId userId,
        string taskId,
        ObjectId actorId,
        string actorRole,
        string timezone)
    {
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanDeleteTasks);
        var routine = await GetOrCreateTomorrowAsync(userId, timezone);
        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada no plano de amanha.");

        var isMember = actorRole.Equals("child", StringComparison.OrdinalIgnoreCase);
        if (isMember && !task.CreatedByMember)
            throw new UnauthorizedAccessException("Tarefas combinadas nao podem ser removidas por este perfil.");

        task.DeletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        routine.Stats = BuildStats(routine.Tasks);
        routine.TomorrowPlanConfirmedAt = null;
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> ReorderTomorrowTasksAsync(
        ObjectId userId,
        List<string> orderedTaskIds,
        ObjectId actorId,
        string actorRole,
        string timezone)
    {
        await EnsureChildPermissionAsync(userId, actorRole, p => p.CanReorderTasks);
        var routine = await GetOrCreateTomorrowAsync(userId, timezone);
        var currentIds = routine.Tasks.Where(t => t.DeletedAt is null).Select(t => t.Id).ToHashSet();

        if (!currentIds.SetEquals(orderedTaskIds))
            throw new ValidationException("A ordenacao precisa conter exatamente as tarefas do plano de amanha.");

        for (var i = 0; i < orderedTaskIds.Count; i++)
        {
            var task = routine.Tasks.First(t => t.Id == orderedTaskIds[i]);
            task.Order = i + 1;
            task.PlannedBy = actorRole.Equals("child", StringComparison.OrdinalIgnoreCase) ? "member" : "adult";
            task.UpdatedAt = DateTime.UtcNow;
        }

        routine.Tasks = routine.Tasks.OrderBy(t => t.Order).ToList();
        routine.TomorrowPlanConfirmedAt = null;
        await _dailyRoutineRepository.UpdateAsync(routine);
        return routine;
    }

    public async Task<DailyRoutine> ConfirmTomorrowAsync(
        ObjectId userId,
        ObjectId actorId,
        string actorRole,
        string timezone)
    {
        var routine = await GetOrCreateTomorrowAsync(userId, timezone);
        routine.TomorrowPlanConfirmedAt = DateTime.UtcNow;
        await _dailyRoutineRepository.UpdateAsync(routine);
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
                MinimumGoalLabel = template.MinimumGoalLabel,
                CompletedAt = null,
                CreatedBy = userId.ToString(),
                Origin = "template",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PlannedBy = routine.Status == RoutineStatus.Planned ? "adult" : null,
                CreatedByMember = false,
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

    public async Task<DailyRoutine> CreateRoutineForDateAsync(ObjectId userId, string date, string timezone, RoutineStatus initialStatus = RoutineStatus.Open)
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
                MinimumGoalLabel = pair.Template.MinimumGoalLabel,
                CompletedAt = null,
                CreatedBy = userId.ToString(),
                Origin = "template",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PlannedBy = initialStatus == RoutineStatus.Planned ? "adult" : null,
                CreatedByMember = false,
            }).ToList();

        var routine = new DailyRoutine
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = userId,
            Date = date,
            Timezone = timezone,
            Status = initialStatus,
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
        TaskValidation.ValidatePoints(request.Points);
        TaskValidation.ValidateTitle(request.Title);
        TaskValidation.ValidateDescription(request.Description);
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
        TaskValidation.ValidatePoints(newPoints);

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
        TaskValidation.ValidateTitle(request.Title);
        TaskValidation.ValidateDescription(request.Description);
        if (!Enum.TryParse<TaskType>(request.Type, true, out var type))
            throw new ValidationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, true, out var period))
            throw new ValidationException($"Periodo invalido: {request.Period}");
        TaskValidation.ValidatePoints(request.Points);
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

    public async Task<DailyRoutine> ConsumeGameTimerAsync(ObjectId userId, int minutes, ObjectId actorId, string actorRole)
    {
        if (minutes <= 0)
            throw new ValidationException("Os minutos da sessao devem ser maiores que zero.");

        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        await SyncGameTimerAsync(routine, userId);

        var remainingMinutes = Math.Max(0, routine.GameTimerMinutes + routine.GameTimerExtraMinutes);
        if (remainingMinutes <= 0)
            throw new ValidationException("O tempo de tela de hoje ja acabou.");

        var consumedMinutes = Math.Min(minutes, remainingMinutes);
        routine.GameTimerExtraMinutes -= consumedMinutes;

        // A nova UX usa o timer antigo apenas como carteira de minutos. Mantemos
        // pausado para que o saldo nao continue correndo sozinho entre sessoes.
        routine.GameTimerPausedAt ??= DateTime.UtcNow;

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

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md). Bonus pequeno e
    // deliberadamente menor que a maioria dos Points de tarefa -- o ponto nao e pagar
    // pela iniciativa, e so reconhece-la um pouco mais que o "so terminei" normal.
    // Nenhum bonus quando um adulto precisou lembrar (a tarefa em si continua valendo
    // os Points normais via ToggleTaskAsync).
    public const int InitiativeBonusSelfStarted = 2;
    public const int InitiativeBonusPromptedByPacus = 1;

    // A crianca monta o "combinado" da tarde/noite -- ver docs/ESTADO_ATUAL.md, item 1.
    // Substitui qualquer plano anterior do mesmo dia (nao acumula); items vazio limpa o
    // plano. Sem RequireRole aqui de proposito, igual ReorderTasksAsync -- e autonomia
    // da propria crianca sobre o dia atual, um adulto tambem pode ajudar a montar.
    public async Task<DailyRoutine> SetEveningPlanAsync(
        ObjectId userId, List<EveningPlanItemRequest> items, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        var validTaskIds = routine.Tasks
            .Where(t => t.DeletedAt is null)
            .Select(t => t.Id)
            .ToHashSet();

        foreach (var item in items)
        {
            if (!validTaskIds.Contains(item.TaskId))
                throw new ValidationException($"Tarefa {item.TaskId} nao encontrada na rotina atual.");
        }

        routine.EveningPlan = items
            .Select((item, index) => new EveningPlanItem
            {
                TaskId = item.TaskId,
                ApproxLabel = string.IsNullOrWhiteSpace(item.ApproxLabel) ? null : item.ApproxLabel.Trim(),
                Order = index,
            })
            .ToList();
        routine.EveningPlanSetAt = DateTime.UtcNow;

        await _dailyRoutineRepository.UpdateAsync(routine);

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = null,
            EventType = TaskEventType.EveningPlanSet,
            ActorId = actorId,
            ActorRole = ParseRole(actorRole),
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    // Autodeclaracao de como a tarefa foi comecada (item 4 da spec: "Incentivar
    // iniciativa"). Concede um pequeno bonus de pontos via PointsService quando a
    // iniciativa nao dependeu de um adulto -- reaproveita o mesmo mecanismo de
    // PointTransaction usado por ToggleTaskAsync, so com um Reason proprio pra ficar
    // claro no extrato que aquele credito e sobre iniciativa, nao sobre a tarefa em si.
    public async Task<DailyRoutine> SetTaskInitiativeAsync(
        ObjectId userId, string taskId, TaskInitiativeLevel initiative, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

        var alreadyInformed = task.Initiative is not null;
        task.Initiative = initiative;
        task.UpdatedAt = DateTime.UtcNow;
        await _dailyRoutineRepository.UpdateAsync(routine);

        var actorRoleEnum = ParseRole(actorRole);

        // So concede o bonus na primeira vez que a crianca informa (evita farmar pontos
        // reabrindo o chip varias vezes pra mesma tarefa).
        if (!alreadyInformed)
        {
            var bonus = initiative switch
            {
                TaskInitiativeLevel.SelfStarted => InitiativeBonusSelfStarted,
                TaskInitiativeLevel.PromptedByPacus => InitiativeBonusPromptedByPacus,
                _ => 0,
            };

            if (bonus > 0)
            {
                await _pointsService.RecordAsync(
                    userId,
                    routine.Id,
                    routine.Date,
                    task.Id,
                    task.Title,
                    PointTransactionType.Award,
                    bonus,
                    actorId,
                    actorRoleEnum,
                    reason: "Bonus de autonomia: iniciativa propria");
            }
        }

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.InitiativeSet,
            ActorId = actorId,
            ActorRole = actorRoleEnum,
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    // Autodeclaracao de por que uma tarefa nao foi feita (item 5 da spec: "Nao utilizar
    // punicao"). Nunca mexe em Points nem em Status -- so registra a resposta pra dar
    // visibilidade de por que certas tarefas ficam pra tras, sem julgar.
    public async Task<DailyRoutine> SetTaskSkipReasonAsync(
        ObjectId userId, string taskId, TaskSkipReason reason, string? note, ObjectId actorId, string actorRole)
    {
        var routine = await _dailyRoutineRepository.GetLatestOpenAsync(userId)
            ?? throw new ValidationException("Nenhuma rotina em aberto para este usuario.");

        var task = routine.Tasks.FirstOrDefault(t => t.Id == taskId && t.DeletedAt is null)
            ?? throw new NotFoundException($"Tarefa {taskId} nao encontrada na rotina atual.");

        task.SkipReason = reason;
        task.SkipReasonNote = reason == TaskSkipReason.Other && !string.IsNullOrWhiteSpace(note)
            ? note.Trim()
            : null;
        task.UpdatedAt = DateTime.UtcNow;

        await _dailyRoutineRepository.UpdateAsync(routine);

        await _taskEventRepository.CreateAsync(new TaskEvent
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            DailyRoutineId = routine.Id,
            TaskId = task.Id,
            TaskTemplateId = TryParseObjectId(task.TaskTemplateId),
            EventType = TaskEventType.SkipReasonSet,
            ActorId = actorId,
            ActorRole = ParseRole(actorRole),
            CreatedAt = DateTime.UtcNow,
        });

        return routine;
    }

    private async Task SyncGameTimerAsync(DailyRoutine routine, ObjectId userId)
    {
        var settings = await _settingsRepository.GetByUserIdAsync(userId);
        routine.GameTimerEnabled = settings?.GameTimerEnabled ?? false;
        routine.GameTimerMinutes = settings?.GameTimerMinutes ?? 120;

        if (routine.GameTimerUnlockedAt is not null || !routine.GameTimerEnabled)
            return;

        // O saldo diario fica disponivel assim que a rotina e carregada. Ele nasce
        // pausado e so e debitado quando uma sessao escolhida termina, evitando que
        // as 2h corram so porque a tela foi aberta.
        var now = DateTime.UtcNow;
        routine.GameTimerUnlockedAt = now;
        routine.GameTimerPausedAt = now;
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
