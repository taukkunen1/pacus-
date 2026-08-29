using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog log);

    // Todos os logs da familia, para exportacao de dados (B2).
    Task<List<AuditLog>> GetAllByFamilyAsync(ObjectId familyId);
}
