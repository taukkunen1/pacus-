using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pacus.Domain.Entities;

public class StoreItem
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }
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
