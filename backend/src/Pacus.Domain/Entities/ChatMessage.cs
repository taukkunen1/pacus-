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

    // V2: mensagens podem representar pedidos rapidos da crianca.
    // Kind = "message" | "request".
    public string Kind { get; set; } = "message";
    // RequestType = "help" | "change_task" | "extra_time".
    public string? RequestType { get; set; }
    // RequestStatus = "pending" | "processing" | "approved" | "rejected".
    public string? RequestStatus { get; set; }
    public int? RequestedMinutes { get; set; }
    public ObjectId? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
