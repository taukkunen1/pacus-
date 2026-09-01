using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class TaskTemplateService : ITaskTemplateService
{
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public TaskTemplateService(
        ITaskTemplateRepository taskTemplateRepository,
        IAuditLogRepository auditLogRepository)
    {
        _taskTemplateRepository = taskTemplateRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<TaskTemplate> CreateAsync(
        ObjectId familyId,
        ObjectId createdBy,
        CreateTaskRequest request)
    {
        var (type, period) = ParseTypeAndPeriod(request);
        var (recurrence, variants) = ParseRecurrenceAndVariants(request);

        var existing = await _taskTemplateRepository.GetActiveByUserAsync(familyId);

        var template = new TaskTemplate
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Period = period,
            Points = request.Points,
            Order = existing.Count + 1,
            Active = true,
            Recurrence = recurrence,
            Variants = variants,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await _taskTemplateRepository.CreateAsync(template);
    }

    public async Task<TaskTemplate> UpdateAsync(
        ObjectId familyId,
        string id,
        CreateTaskRequest request)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        var (type, period) = ParseTypeAndPeriod(request);
        var (recurrence, variants) = ParseRecurrenceAndVariants(request);

        template.Title = request.Title;
        template.Description = request.Description;
        template.Type = type;
        template.Period = period;
        template.Points = request.Points;
        template.Recurrence = recurrence;
        template.Variants = variants;
        template.UpdatedAt = DateTime.UtcNow;

        await _taskTemplateRepository.UpdateAsync(template);

        return template;
    }

    public async Task ActivateAsync(
        ObjectId familyId,
        string id)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        await _taskTemplateRepository.ActivateAsync(templateId);
    }

    public async Task DeleteAsync(
        ObjectId familyId,
        string id,
        ObjectId actorId,
        string actorRole)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        // Mesma checagem de posse usada em Update/Activate: sem isso, qualquer adulto
        // autenticado (independente da familia) poderia excluir o template de outra
        // familia so sabendo (ou adivinhando) o ObjectId. Achado em auditoria de
        // seguranca (isolamento por FamilyId) — nunca chamar o repositorio direto
        // a partir do controller para operacoes que envolvem posse.
        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        await _taskTemplateRepository.SoftDeleteAsync(templateId);

        // Log de auditoria (checklist de seguranca, item A5) — exclusao de tarefa
        // permanente e uma acao administrativa sensivel, registrada separada do
        // dado em si (colecao audit_logs, nunca tocada pelo fluxo normal do app).
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Action = "task_template.deleted",
            EntityType = "TaskTemplate",
            EntityId = templateId.ToString(),
            Details = $"Tarefa excluida: {template.Title}",
            ActorId = actorId,
            ActorRole = actorRole.Equals("adult", StringComparison.OrdinalIgnoreCase) ? UserRole.Adult : UserRole.Child,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static (TaskType Type, TaskPeriod Period) ParseTypeAndPeriod(
        CreateTaskRequest request)
    {
        if (!Enum.TryParse<TaskType>(
                request.Type,
                ignoreCase: true,
                out var type))
        {
            throw new InvalidOperationException(
                $"Tipo de tarefa invalido: {request.Type}");
        }

        if (!Enum.TryParse<TaskPeriod>(
                request.Period,
                ignoreCase: true,
                out var period))
        {
            throw new InvalidOperationException(
                $"Periodo invalido: {request.Period}");
        }

        if (request.Points == 0 || request.Points < -10 || request.Points > 10)
        {
            throw new InvalidOperationException(
                "Cada tarefa deve valer entre 1 e 10 Pacus Points, ou entre -1 e -10 (penalidade). Zero nao e permitido.");
        }

        return (type, period);
    }

    private static readonly HashSet<string> ValidRecurrences = new(StringComparer.OrdinalIgnoreCase)
    {
        TaskTemplate.RecurrenceDaily,
        TaskTemplate.RecurrenceWeekday,
        TaskTemplate.RecurrenceWeekend,
        TaskTemplate.RecurrenceWeekdayRotation,
    };

    private static (string Recurrence, List<TaskTemplateVariant> Variants) ParseRecurrenceAndVariants(
        CreateTaskRequest request)
    {
        var recurrence = string.IsNullOrWhiteSpace(request.Recurrence)
            ? TaskTemplate.RecurrenceDaily
            : request.Recurrence;

        if (!ValidRecurrences.Contains(recurrence))
        {
            throw new InvalidOperationException(
                $"Recorrencia invalida: {recurrence}. Use {TaskTemplate.RecurrenceDaily}, {TaskTemplate.RecurrenceWeekday}, {TaskTemplate.RecurrenceWeekend} ou {TaskTemplate.RecurrenceWeekdayRotation}.");
        }

        if (!recurrence.Equals(TaskTemplate.RecurrenceWeekdayRotation, StringComparison.OrdinalIgnoreCase))
            return (recurrence, new List<TaskTemplateVariant>());

        if (request.Variants is null || request.Variants.Count == 0)
        {
            throw new InvalidOperationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceWeekdayRotation}\" precisa de pelo menos uma variante (Variants).");
        }

        var variants = new List<TaskTemplateVariant>();
        var seenDays = new HashSet<DayOfWeek>();

        foreach (var variant in request.Variants)
        {
            if (!Enum.TryParse<DayOfWeek>(variant.DayOfWeek, ignoreCase: true, out var dayOfWeek))
            {
                throw new InvalidOperationException(
                    $"Dia da semana invalido na variante: {variant.DayOfWeek}.");
            }

            // So segunda a sexta -- e o proposito desta recorrencia (fim de semana usa
            // RecurrenceWeekend/RecurrenceDaily se precisar de sabado/domingo tambem).
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                throw new InvalidOperationException(
                    $"Recorrencia \"{TaskTemplate.RecurrenceWeekdayRotation}\" so aceita dias uteis (segunda a sexta); recebido: {dayOfWeek}.");
            }

            if (!seenDays.Add(dayOfWeek))
            {
                throw new InvalidOperationException(
                    $"Dia da semana repetido nas variantes: {dayOfWeek}.");
            }

            if (string.IsNullOrWhiteSpace(variant.Title))
            {
                throw new InvalidOperationException(
                    $"Toda variante precisa de titulo (faltando em {dayOfWeek}).");
            }

            variants.Add(new TaskTemplateVariant
            {
                DayOfWeek = dayOfWeek,
                Title = variant.Title,
                Description = variant.Description,
            });
        }

        return (recurrence, variants);
    }
}