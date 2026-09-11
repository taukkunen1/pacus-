using Pacus.Domain.Enums;

namespace Pacus.Application.DTOs;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md).

public record SetTaskInitiativeRequest(TaskInitiativeLevel Initiative);

public record SetTaskSkipReasonRequest(
    TaskSkipReason Reason,
    // So usado quando Reason == TaskSkipReason.Other. Ignorado nos demais casos.
    string? Note = null
);

public record EveningPlanItemRequest(
    string TaskId,
    // Momento aproximado escolhido pela crianca (ex.: "depois do banho"), texto
    // livre e opcional -- nao e uma hora exata.
    string? ApproxLabel = null
);

public record SetEveningPlanRequest(List<EveningPlanItemRequest> Items);

public record EveningPlanItemResponse(string TaskId, string? ApproxLabel, int Order);

// Relatorio semanal de autonomia (item 6 da spec): mostra evolucao de
// independencia, nao so quantidade de tarefas concluidas -- quantas tarefas a
// crianca comecou sozinha, quantas precisaram de uma sugestao do PACUS, e quantas
// precisaram de lembrete de adulto, nos ultimos 7 dias operacionais (incluindo
// hoje) comparado aos 7 dias anteriores.
public record AutonomyWeeklyReportResponse(
    string FromDate,
    string ToDate,
    int SelfStarted,
    int PromptedByPacus,
    int PromptedByAdult,
    int NotYetInformed,
    // Mesmos campos, mas da semana imediatamente anterior -- pra dar uma nocao de
    // tendencia ("precisando de menos ajuda") sem exigir que o frontend calcule
    // isso sozinho.
    int PreviousSelfStarted,
    int PreviousPromptedByPacus,
    int PreviousPromptedByAdult
);

