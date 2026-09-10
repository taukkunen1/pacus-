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

    // Atualiza o FamilyCode/Timezone de todos os membros da familia numa unica
    // operacao (revisao de melhorias, 2026-09-10) -- antes FamilyController fazia
    // um GetByFamilyAsync + loop com UpdateAsync por membro (N+1: um round-trip ao
    // Mongo por pessoa da familia so pra propagar o mesmo valor pra todo mundo).
    Task UpdateFamilyCodeForFamilyAsync(ObjectId familyId, string familyCode);
    Task UpdateTimezoneForFamilyAsync(ObjectId familyId, string timezone);

    // Remove todos os usuarios da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
