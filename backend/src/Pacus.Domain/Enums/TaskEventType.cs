namespace Pacus.Domain.Enums;

public enum TaskEventType
{
    Created,
    Updated,
    Deleted,
    Completed,
    Reopened,
    Reordered,
    PointsProposed,
    PointsAdjusted,
    OptionSelected,

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md).
    InitiativeSet,
    SkipReasonSet,
    EveningPlanSet,
}
