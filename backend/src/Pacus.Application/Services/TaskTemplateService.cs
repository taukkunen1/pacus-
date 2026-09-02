using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Application.Exceptions;

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
        var (recurrence, variants, customDays, anchorDate, intervalDays) = ParseRecurrenceAndVariants(request);
        var options = ParseOptions(request.Options);
        var reasons = ParseReasons(request.Reasons, request.Reason);

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
            AnchorDate = anchorDate,
            IntervalDays = intervalDays,
            Options = options,
            Reasons = reasons,
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
            throw new ValidationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new NotFoundException("Tarefa permanente nao encontrada.");

        var (type, period) = ParseTypeAndPeriod(request);
        var (recurrence, variants, customDays, anchorDate, intervalDays) = ParseRecurrenceAndVariants(request);
        var options = ParseOptions(request.Options);
        var reasons = ParseReasons(request.Reasons, request.Reason);

        template.Title = request.Title;
        template.Description = request.Description;
        template.Type = type;
        template.Period = period;
        template.Points = request.Points;
        template.Recurrence = recurrence;
        template.Variants = variants;
        template.CustomDays = customDays;
        template.AnchorDate = anchorDate;
        template.IntervalDays = intervalDays;
        template.Options = options;
        // Reasons e a fonte de verdade a partir de agora; zera o campo legado pra
        // nao deixar as duas copias divergirem depois de uma edicao (ver
        // TaskTemplate.EffectiveReasons -- so cai pro legado quando Reasons nunca
        // foi regravado desde esta mudanca).
        template.Reasons = reasons;
        template.Reason = null;
        template.UpdatedAt = DateTime.UtcNow;

        await _taskTemplateRepository.UpdateAsync(template);

        return template;
    }

    public async Task ActivateAsync(
        ObjectId familyId,
        string id)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new ValidationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new NotFoundException("Tarefa permanente nao encontrada.");

        await _taskTemplateRepository.ActivateAsync(templateId);
    }

    public async Task DeleteAsync(
        ObjectId familyId,
        string id,
        ObjectId actorId,
        string actorRole)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new ValidationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        // Mesma checagem de posse usada em Update/Activate: sem isso, qualquer adulto
        // autenticado (independente da familia) poderia excluir o template de outra
        // familia so sabendo (ou adivinhando) o ObjectId. Achado em auditoria de
        // seguranca (isolamento por FamilyId) — nunca chamar o repositorio direto
        // a partir do controller para operacoes que envolvem posse.
        if (template is null || template.FamilyId != familyId)
            throw new NotFoundException("Tarefa permanente nao encontrada.");

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
            throw new ValidationException(
                $"Tipo de tarefa invalido: {request.Type}");
        }

        if (!Enum.TryParse<TaskPeriod>(
                request.Period,
                ignoreCase: true,
                out var period))
        {
            throw new ValidationException(
                $"Periodo invalido: {request.Period}");
        }

        if (request.Points == 0 || request.Points < -10 || request.Points > 10)
        {
            throw new ValidationException(
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
        TaskTemplate.RecurrenceInterval,
    };

    private static (string Recurrence, List<TaskTemplateVariant> Variants, List<DayOfWeek> CustomDays, string? AnchorDate, int IntervalDays) ParseRecurrenceAndVariants(
        CreateTaskRequest request)
    {
        var recurrence = string.IsNullOrWhiteSpace(request.Recurrence)
            ? TaskTemplate.RecurrenceDaily
            : request.Recurrence;

        if (!ValidRecurrences.Contains(recurrence))
        {
            throw new ValidationException(
                $"Recorrencia invalida: {recurrence}. Use {TaskTemplate.RecurrenceDaily}, {TaskTemplate.RecurrenceWeekday}, {TaskTemplate.RecurrenceWeekend}, {TaskTemplate.RecurrenceCustom}, {TaskTemplate.RecurrenceInterval} ou {TaskTemplate.RecurrenceWeekdayRotation}.");
        }

        if (recurrence.Equals(TaskTemplate.RecurrenceCustom, StringComparison.OrdinalIgnoreCase))
            return (recurrence, new List<TaskTemplateVariant>(), ParseCustomDays(request.CustomDays), null, 2);

        if (recurrence.Equals(TaskTemplate.RecurrenceInterval, StringComparison.OrdinalIgnoreCase))
        {
            var (anchorDate, intervalDays) = ParseIntervalRecurrence(request.AnchorDate, request.IntervalDays);
            return (recurrence, new List<TaskTemplateVariant>(), new List<DayOfWeek>(), anchorDate, intervalDays);
        }

        if (!recurrence.Equals(TaskTemplate.RecurrenceWeekdayRotation, StringComparison.OrdinalIgnoreCase))
            return (recurrence, new List<TaskTemplateVariant>(), new List<DayOfWeek>(), null, 2);

        if (request.Variants is null || request.Variants.Count == 0)
        {
            throw new ValidationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceWeekdayRotation}\" precisa de pelo menos uma variante (Variants).");
        }

        var variants = new List<TaskTemplateVariant>();
        var seenDays = new HashSet<DayOfWeek>();

        foreach (var variant in request.Variants)
        {
            if (!Enum.TryParse<DayOfWeek>(variant.DayOfWeek, ignoreCase: true, out var dayOfWeek))
            {
                throw new ValidationException(
                    $"Dia da semana invalido na variante: {variant.DayOfWeek}.");
            }

            // So segunda a sexta -- e o proposito desta recorrencia (fim de semana usa
            // RecurrenceWeekend/RecurrenceDaily se precisar de sabado/domingo tambem).
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                throw new ValidationException(
                    $"Recorrencia \"{TaskTemplate.RecurrenceWeekdayRotation}\" so aceita dias uteis (segunda a sexta); recebido: {dayOfWeek}.");
            }

            if (!seenDays.Add(dayOfWeek))
            {
                throw new ValidationException(
                    $"Dia da semana repetido nas variantes: {dayOfWeek}.");
            }

            if (string.IsNullOrWhiteSpace(variant.Title))
            {
                throw new ValidationException(
                    $"Toda variante precisa de titulo (faltando em {dayOfWeek}).");
            }

            if (variant.Points is { } variantPoints && (variantPoints == 0 || variantPoints < -10 || variantPoints > 10))
            {
                throw new ValidationException(
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

        return (recurrence, variants, new List<DayOfWeek>(), null, 2);
    }

    // RecurrenceInterval ("dia sim, dia nao" ou qualquer intervalo de N dias):
    // precisa de uma data-ancora valida (formato "yyyy-MM-dd") e um intervalo >= 1.
    // IntervalDays default 2 quando nao informado -- e o caso mais comum ("dia sim,
    // dia nao"); 1 equivaleria a RecurrenceDaily, mas nao bloqueamos isso aqui (o
    // dono da familia pode ter um motivo, e o resultado seria so redundante, nunca
    // incorreto).
    private static (string AnchorDate, int IntervalDays) ParseIntervalRecurrence(string? anchorDate, int? intervalDays)
    {
        if (string.IsNullOrWhiteSpace(anchorDate))
        {
            throw new ValidationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceInterval}\" precisa de uma data de inicio (AnchorDate, formato yyyy-MM-dd).");
        }

        if (!DateTime.TryParseExact(
                anchorDate,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _))
        {
            throw new ValidationException(
                $"Data de inicio invalida: {anchorDate}. Use o formato yyyy-MM-dd.");
        }

        var interval = intervalDays ?? 2;
        if (interval < 1)
        {
            throw new ValidationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceInterval}\" precisa de um intervalo de pelo menos 1 dia (recebido: {interval}).");
        }

        return (anchorDate, interval);
    }

    // RecurrenceCustom aceita qualquer combinacao de dias (inclusive so um, como
    // "Escoteiro" so sabado, ou so dois no meio da semana, como "Ingles" so terca
    // e quarta) -- diferente de RecurrenceWeekdayRotation, aqui e o MESMO
    // titulo/descricao do template em todos os dias escolhidos.
    private static List<DayOfWeek> ParseCustomDays(List<string>? customDays)
    {
        if (customDays is null || customDays.Count == 0)
        {
            throw new ValidationException(
                $"Recorrencia \"{TaskTemplate.RecurrenceCustom}\" precisa de pelo menos um dia (CustomDays).");
        }

        var days = new List<DayOfWeek>();
        var seen = new HashSet<DayOfWeek>();

        foreach (var raw in customDays)
        {
            if (!Enum.TryParse<DayOfWeek>(raw, ignoreCase: true, out var day))
            {
                throw new ValidationException($"Dia da semana invalido: {raw}.");
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
            throw new ValidationException("Nenhuma opcao pode ficar em branco.");

        if (options.Count < 2 || options.Count > 4)
            throw new ValidationException("Uma tarefa com opcoes precisa de 2 a 4 opcoes.");

        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            throw new ValidationException("As opcoes nao podem se repetir.");

        return options;
    }

    // So trim + null-se-vazio, sem pool -- usado nos dois lugares que editam UM
    // DailyTask especifico diretamente (CreateAdHocTaskAsync/UpdateTaskAsync em
    // DailyRoutineService, via DailyTaskUpdateRequest.Reason), nao um TaskTemplate
    // inteiro. Um unico dia nao tem "variedade" pra sortear -- so faz sentido a
    // pessoa escrever um motivo especifico pra aquela tarefa daquele dia.
    public static string? ParseSingleReason(string? rawReason)
    {
        var trimmed = rawReason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    // "Por que isso importa" (TaskTemplate.Reasons) -- pool de frases que
    // DailyRoutineService sorteia uma por dia (pedido do dono do produto,
    // 2026-09-02: motivos variados em vez da mesma frase todo dia). Aceita
    // rawReasons (campo atual) OU legacyReason (campo antigo, string unica --
    // mantido pra clientes/testes que ainda mandam so "reason") -- rawReasons tem
    // prioridade quando os dois vierem preenchidos. Sem limite de quantidade
    // artificial tipo Options (nao sao "escolhas" que a crianca compara lado a
    // lado, sao so variantes da mesma explicacao), mas um teto generoso de 8 evita
    // que a lista vire ruido. Frases duplicadas (ignorando maiusculas/espacos) sao
    // descartadas silenciosamente -- nao ha por que sortear entre duas copias da
    // mesma frase.
    public static List<string> ParseReasons(List<string>? rawReasons, string? legacyReason)
    {
        List<string> source;
        if (rawReasons is not null && rawReasons.Count > 0)
            source = rawReasons;
        else if (!string.IsNullOrWhiteSpace(legacyReason))
            source = new List<string> { legacyReason };
        else
            return new List<string>();

        var reasons = source
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (reasons.Count > 8)
            throw new ValidationException("No maximo 8 motivos por tarefa.");

        return reasons;
    }
}
