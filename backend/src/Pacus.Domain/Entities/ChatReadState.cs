using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class ChatReadState
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public ObjectId UserId { get; set; }
    public ObjectId LastReadMessageId { get; set; }
    public DateTime LastReadAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
