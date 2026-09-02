namespace Pacus.Application.DTOs;

// Recurrence, Variants e CustomDays sao opcionais (default "daily" / null) pra nao
// quebrar chamadores existentes que nunca mandavam esses campos (ex.: criacao de
// tarefa so pra hoje em DailyTasksController, que ignora os tres). Ver
// TaskTemplate.Recurrence* pros valores aceitos.
public record CreateTaskRequest(
    string Title,
    string? Description,
    string Type,
    string Period,
    int Points,
    string Recurrence = "daily",
    List<TaskVariantRequest>? Variants = null,
    // So usado quando Recurrence == "custom" -- nomes de DayOfWeek (ex.: "tuesday",
    // "wednesday"), qualquer combinacao incluindo fim de semana.
    List<string>? CustomDays = null,
    // So usados quando Recurrence == "interval" -- ver TaskTemplate.AnchorDate/
    // IntervalDays. AnchorDate no formato "yyyy-MM-dd"; IntervalDays default 2
    // ("dia sim, dia nao") quando nao informado.
    string? AnchorDate = null,
    int? IntervalDays = null,
    // Op-in de escolha (Teoria da Autodeterminacao, ver docs/PROPOSITO.md): 2-4 opcoes
    // curtas que a crianca escolhe entre si antes de concluir. Null/vazio = sem opcoes.
    List<string>? Options = null,
    // Legado -- mantido so pra clientes antigos que ainda mandam um unico "reason".
    // Ver TaskTemplate.Reason. Ignorado quando Reasons abaixo vem preenchido.
    string? Reason = null,
    // "Por que isso importa" -- ver TaskTemplate.Reasons. Pool de frases pertinentes;
    // DailyRoutineService sorteia uma diferente a cada dia gerado. Null/vazio = sem
    // motivo explicito.
    List<string>? Reasons = null
);

public record TaskVariantRequest(
    string DayOfWeek,
    string Title,
    string? Description,
    // Opcional -- null usa os Points do template (mesma pontuacao todo dia).
    // Preenchido, so essa variante vale diferente (ex.: uma missao que exige
    // supervisao de adulto vale mais que uma rapida e sozinha).
    int? Points = null
);

public record AdjustPointsRequest(int Points);
