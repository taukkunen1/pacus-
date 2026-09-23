using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class ChatMessage
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public ObjectId SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public UserRole SenderRole { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
