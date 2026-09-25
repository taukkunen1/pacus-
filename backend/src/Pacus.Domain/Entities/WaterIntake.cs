using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class WaterIntake
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public ObjectId ActorId { get; set; }
    public string Date { get; set; } = string.Empty;
    public int AmountMl { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
