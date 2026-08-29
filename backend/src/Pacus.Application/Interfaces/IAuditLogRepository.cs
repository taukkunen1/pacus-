using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog log);
}
