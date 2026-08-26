using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class PacusGrowthLog
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public ObjectId PacusId { get; set; }
    public string Date { get; set; } = string.Empty;
    public ObjectId DailyRoutineId { get; set; }
    public PacusStage StageBefore { get; set; }
    public PacusStage StageAfter { get; set; }
    public double SizeBefore { get; set; }
    public double SizeAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}
