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
    List<string>? CustomDays = null
);

public record TaskVariantRequest(
    string DayOfWeek,
    string Title,
    string? Description
);

public record AdjustPointsRequest(int Points);
