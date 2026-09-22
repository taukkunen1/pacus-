namespace Pacus.Domain.Enums;

public enum RoutineStatus
{
    // Explicit values preserve existing MongoDB enum integers.
    Open = 0,
    Closed = 1,
    Planned = 2
}
