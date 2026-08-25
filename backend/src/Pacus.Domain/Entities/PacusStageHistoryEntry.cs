using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class PacusStageHistoryEntry
{
    public PacusStage Stage { get; set; }
    public DateTime ReachedAt { get; set; }
}
