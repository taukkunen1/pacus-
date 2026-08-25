using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class TaskEvent
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public ObjectId? DailyRoutineId { get; set; }
    public string? TaskId { get; set; }
    public ObjectId? TaskTemplateId { get; set; }
    public TaskEventType EventType { get; set; }
    public BsonDocument? Payload { get; set; } // { before, after }
    public ObjectId ActorId { get; set; }
    public UserRole ActorRole { get; set; }
    public DateTime CreatedAt { get; set; }
}
