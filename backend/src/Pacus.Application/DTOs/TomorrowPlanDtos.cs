namespace Pacus.Application.DTOs;

// MVP "Meu Amanhã": tarefas próprias, autoria, período, ordem e plano concreto.
public record TomorrowTaskRequest(
    string Title,
    string? Description,
    string Period,
    string? PlanCue = null
);

public record UpdateTomorrowTaskRequest(
    string Title,
    string? Description,
    string Period,
    string? PlanCue = null
);
