using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class GrowthStageConfig
{
    public PacusStage Stage { get; set; }
    // Formato YYYY-MM-DD
    public string Date { get; set; } = string.Empty;
}
