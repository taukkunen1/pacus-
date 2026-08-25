using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class TaskTemplate
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public TaskPeriod Period { get; set; }
    public int Points { get; set; }
    public int Order { get; set; }
    public bool Active { get; set; } = true;
    public string Recurrence { get; set; } = "daily"; // daily | weekday | weekend | custom
    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
