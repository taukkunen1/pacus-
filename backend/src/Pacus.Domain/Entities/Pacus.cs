using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class Pacus
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public PacusStage Stage { get; set; }
    public double Size { get; set; }
    public int TotalClosedDays { get; set; }
    public string? LastGrowthDate { get; set; }
    public List<PacusStageHistoryEntry> StageHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
