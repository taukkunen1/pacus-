using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

// Log de auditoria para acoes administrativas sensiveis (checklist de
// seguranca, item A5): quem fez, quando, e o que mudou -- separado do dado
// em si (a colecao "audit_logs" nao e alterada pela acao normal do app,
// so por este log, que nunca deve ser editado ou removido pelo fluxo
// normal). Cobre hoje: exclusao de tarefa permanente, aprovacao/rejeicao
// de resgate, e ajuste manual de saldo de pontos.
public class AuditLog
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }

    // Identificador curto e estavel da acao, ex. "task_template.deleted",
    // "redemption.approved", "points.manual_adjustment".
    public string Action { get; set; } = string.Empty;

    // Tipo e id da entidade afetada, ex. "TaskTemplate" / "<ObjectId>".
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    // Detalhe legivel por humano da mudanca (ex. "saldo: 50 -> 80 (motivo: X)").
    public string? Details { get; set; }

    public ObjectId ActorId { get; set; }
    public UserRole ActorRole { get; set; }
    public DateTime CreatedAt { get; set; }

    // Anonimizacao pos-exclusao de conta (LGPD, item B3): quando a familia e excluida, o
    // log em si e preservado por um periodo (legitimo interesse -- responsabilizacao /
    // prevencao a fraude, art. 7 IX), mas o vinculo direto com a pessoa (ActorId) e
    // removido. PurgeAt marca quando o log pode ser definitivamente apagado (indice TTL).
    public bool Anonymized { get; set; }
    public DateTime? PurgeAt { get; set; }
}
