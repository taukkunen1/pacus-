using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class StoreItem
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Cost { get; set; }
    public string Category { get; set; } = "other"; // screen_time | toy | activity | other
    public string? Icon { get; set; }
    public bool Active { get; set; } = true;
    public int? Stock { get; set; } // null = ilimitado
    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
