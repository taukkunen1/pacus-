using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog log);

    // Todos os logs da familia, para exportacao de dados (B2).
    Task<List<AuditLog>> GetAllByFamilyAsync(ObjectId familyId);

    // Exclusao de conta (LGPD, item B3): remove o vinculo com a pessoa (ActorId) de todos
    // os logs da familia e marca quando podem ser definitivamente apagados, em vez de
    // apagar o log inteiro -- preserva o historico de responsabilizacao por um periodo.
    Task AnonymizeByFamilyAsync(ObjectId familyId, DateTime purgeAt);
}
