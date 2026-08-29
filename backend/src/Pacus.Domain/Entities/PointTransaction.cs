using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class PointTransaction
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }
    public string Date { get; set; } = string.Empty;
    // Nulo para transacoes que nao pertencem a um dia especifico — ex. Redemption (gasto na loja).
    public ObjectId? DailyRoutineId { get; set; }
    // Para Redemption, guarda o id da propria redemption em vez de uma tarefa.
    public string TaskId { get; set; } = string.Empty;
    public string TaskTitle { get; set; } = string.Empty;
    public PointTransactionType Type { get; set; }
    // Delta assinado: Award positivo, Reversal/Redemption negativo, Adjustment +/-
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reason { get; set; }
    public ObjectId ActorId { get; set; }
    public UserRole ActorRole { get; set; }
    public DateTime CreatedAt { get; set; }
}
