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
        var (recurrence, variants, customDays) = ParseRecurrenceAndVariants(request);
        var options = ParseOptions(request.Options);
        var reason = ParseReason(request.Reason);

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
            CustomDays = customDays,
            Options = options,
            Reason = reason,
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
        var (recurrence, variants, customDays) = ParseRecurrenceAndVariants(request);
        var options = ParseOptions(request.Options);
        var reason = ParseReason(request.Reason);

        template.Title = request.Title;
        template.Description = request.Description;
        template.Type = type;
        template.Period = period;
        template.Points = request.Points;
        template.Recurrence = recurrence;
        template.Variants = variants;
        template.CustomDays = customDays;
        template.Options = options;
        template.Reason = reason;
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
        TaskTemplate.RecurrenceCustom,
    };

    private static (string Recurrence, List<TaskTemplateVariant> Variants, List<DayOfWeek> CustomDays) ParseRecurrenceAndVariants(
        CreateTaskRequest request)
    {
        var recurrence = string.IsNullOrWhiteSpace(request.Recurrence)
            ? TaskTemplate.RecurrenceDaily
            : request.Recurrence;

        if (!ValidRecurrences.Contains(recurrence))
        {
            throw new InvalidOperationException(
                $"Recorrencia invalida: {recurrence}. Use {TaskTemplate.RecurrenceDaily}, {TaskTemplate.RecurrenceWeekday}, {TaskTemplate.RecurrenceWeekend}, {TaskTemplate.RecurrenceCustom} ou {TaskTemplate.RecurrenceWeekdayRotation}.");
        }

        if (recurrence.Equals(TaskTemplate.RecurrenceCustom, StringComparison.OrdinalIgnoreCase))
            return (recurrence, new List<TaskTemplateVariant>(), ParseCustomDays(request.CustomDays));

        if (!recurrence.Equals(TaskTemplate.RecurrenceWeekdayRotation, StringComparison.OrdinalIgnoreCase))
            return (recurrence, new List<TaskTemplateVariant>(), new List<DayOfWeek>());

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

            if (variant.Points is { } variantPoints && (variantPoints == 0 || variantPoints < -10 || variantPoints > 10))
            {
                throw new InvalidOperationException(
                    $"Pontos invalidos na variante de {dayOfWeek}: cada tarefa deve valer entre 1 e 10 Pacus Points, ou entre -1 e -10 (penalidade). Zero nao e permitido.");
            }

            variants.Add(new TaskTemplateVariant
            {
                DayOfWeek = dayOfWeek,
                Title = variant.Title,
                Description = variant.Description,
                Points = variant.Points,
            });
        }

        return (recurrence, variants, new List<DayOfWeek>());
    }

    // RecurrenceCustom aceita qualquer combinacao de dias (inclusive so um, como
    // "Escoteiro" so sabado, ou so dois no meio da semana, como "Ingles" so terca
    // e quarta) -- diferente de RecurrenceWeekdayRotation, aqui e o MESMO
    // titulo/descricao do template em todos os dias escolhidos.
    private static List<DayOfWeek> ParseCustomDays(List<string>? customDays)
    {
        if (customDays is null || customDays.Count == 0)
        {
            throw new InvalidOperationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceCustom}\" precisa de pelo menos um dia (CustomDays).");
        }

        var days = new List<DayOfWeek>();
        var seen = new HashSet<DayOfWeek>();

        foreach (var raw in customDays)
        {
            if (!Enum.TryParse<DayOfWeek>(raw, ignoreCase: true, out var day))
            {
                throw new InvalidOperationException($"Dia da semana invalido: {raw}.");
            }

            if (seen.Add(day))
                days.Add(day);
        }

        return days;
    }

    // Op-in de escolha real pra crianca (Teoria da Autodeterminacao -- docs/PROPOSITO.md).
    // Null/vazio = tarefa sem opcoes (comportamento normal). Quando presente, exige 2-4
    // opcoes nao vazias e sem duplicata -- uma unica opcao nao seria "escolha" nenhuma,
    // e mais de 4 vira ruido pra uma crianca decidir.
    public static List<string> ParseOptions(List<string>? rawOptions)
    {
        if (rawOptions is null || rawOptions.Count == 0)
            return new List<string>();

        var options = rawOptions
            .Select(o => o?.Trim() ?? string.Empty)
            .Where(o => o.Length > 0)
            .ToList();

        if (options.Count != rawOptions.Count)
            throw new InvalidOperationException("Nenhuma opcao pode ficar em branco.");

        if (options.Count < 2 || options.Count > 4)
            throw new InvalidOperationException("Uma tarefa com opcoes precisa de 2 a 4 opcoes.");

        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            throw new InvalidOperationException("As opcoes nao podem se repetir.");

        return options;
    }

    // "Por que isso importa" (TaskTemplate.Reason) -- so trim + null-se-vazio, sem
    // limite artificial de tamanho: diferente de Options, aqui e texto livre de
    // verdade (uma frase, geralmente), sem estrutura pra validar.
    public static string? ParseReason(string? rawReason)
    {
        var trimmed = rawReason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}