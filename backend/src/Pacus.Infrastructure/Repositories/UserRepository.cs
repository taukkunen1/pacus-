using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly MongoDbContext _context;

    public UserRepository(MongoDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(ObjectId id) =>
        _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();

    public Task<User?> GetByEmailAsync(string email) =>
        _context.Users.Find(u => u.Email == email).FirstOrDefaultAsync();

    public async Task<User> CreateAsync(User user)
    {
        await _context.Users.InsertOneAsync(user);
        return user;
    }

    public Task UpdateAsync(User user) =>
        _context.Users.ReplaceOneAsync(u => u.Id == user.Id, user);

    public Task<List<User>> GetByFamilyAndRoleAsync(ObjectId familyId, UserRole role) =>
        _context.Users.Find(u => u.FamilyId == familyId && u.Role == role).ToListAsync();
}
