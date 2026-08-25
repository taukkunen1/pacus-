using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class PointTransaction
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
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
