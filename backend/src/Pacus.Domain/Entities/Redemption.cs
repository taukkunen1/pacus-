using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class Redemption
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    public ObjectId StoreItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public int Cost { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Pending;
    public ObjectId RequestedBy { get; set; }
    public ObjectId? ReviewedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ObjectId? PointTransactionId { get; set; }
}
