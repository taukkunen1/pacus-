using MongoDB.Bson;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(ObjectId id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<List<User>> GetByFamilyAndRoleAsync(ObjectId familyId, UserRole role);

    // Todos os membros da familia (adulto + crianca(s)), para exportacao de dados (LGPD, item B2).
    Task<List<User>> GetByFamilyAsync(ObjectId familyId);

    // Remove todos os usuarios da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
