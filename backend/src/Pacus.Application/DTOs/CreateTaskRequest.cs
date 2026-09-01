namespace Pacus.Application.DTOs;

// Recurrence e Variants sao opcionais (default "daily" / null) pra nao quebrar
// chamadores existentes que nunca mandavam esses campos (ex.: criacao de tarefa so
// pra hoje em DailyTasksController, que ignora os dois). Ver TaskTemplate.Recurrence*
// pros valores aceitos.
public record CreateTaskRequest(
    string Title,
    string? Description,
    string Type,
    string Period,
    int Points,
    string Recurrence = "daily",
    List<TaskVariantRequest>? Variants = null
);

public record TaskVariantRequest(
    string DayOfWeek,
    string Title,
    string? Description
);

public record AdjustPointsRequest(int Points);
