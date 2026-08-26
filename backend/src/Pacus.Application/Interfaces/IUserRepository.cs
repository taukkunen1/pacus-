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
}
