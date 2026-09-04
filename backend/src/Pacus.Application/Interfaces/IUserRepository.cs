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

    // Todos os membros com este codigo de familia (adulto + crianca(s)) -- usado
    // pelo login da crianca por codigo (ver User.FamilyCode) e pela checagem de
    // unicidade ao gerar um codigo novo no bootstrap. Lista vazia = codigo nao
    // existe (ou nao esta em uso ainda).
    Task<List<User>> GetByFamilyCodeAsync(string familyCode);

    // Todos os membros da familia (adulto + crianca(s)), para exportacao de dados (LGPD, item B2).
    Task<List<User>> GetByFamilyAsync(ObjectId familyId);

    // Remove todos os usuarios da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
