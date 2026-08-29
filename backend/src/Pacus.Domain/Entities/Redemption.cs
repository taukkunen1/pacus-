using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class Redemption
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }
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
