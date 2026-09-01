namespace Pacus.Application.DTOs;

public record DailyTaskUpdateRequest(
    string Title,
    string? Description,
    string Type,
    string Period,
    int Points,
    List<string>? Options = null,
    // "Por que isso importa" -- ver TaskTemplate.Reason. Opcional, null/vazio = sem motivo.
    string? Reason = null
);

public record SelectTaskOptionRequest(string? SelectedOption);
