using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class Settings
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public double PointToBrlRate { get; set; } = 0.05;
    public List<GrowthStageConfig> GrowthStages { get; set; } = new();
    public ChildPermissions ChildPermissions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
