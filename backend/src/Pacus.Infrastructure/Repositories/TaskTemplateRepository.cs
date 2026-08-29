using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class TaskTemplateRepository : ITaskTemplateRepository
{
    private readonly MongoDbContext _context;

    public TaskTemplateRepository(MongoDbContext context) => _context = context;

    public Task<List<TaskTemplate>> GetActiveByUserAsync(ObjectId userId) =>
        _context.TaskTemplates.Find(t => t.FamilyId == userId && t.Active && t.DeletedAt == null)
            .SortBy(t => t.Order)
            .ToListAsync();

    public Task<TaskTemplate?> GetByIdAsync(ObjectId id) =>
        _context.TaskTemplates.Find(t => t.Id == id).FirstOrDefaultAsync();

    public async Task<TaskTemplate> CreateAsync(TaskTemplate template)
    {
        await _context.TaskTemplates.InsertOneAsync(template);
        return template;
    }

    public Task UpdateAsync(TaskTemplate template) =>
        _context.TaskTemplates.ReplaceOneAsync(t => t.Id == template.Id, template);

    // Soft delete — nunca remove fisicamente; task_events guarda o registro da exclusao.
    public Task SoftDeleteAsync(ObjectId id) =>
        _context.TaskTemplates.UpdateOneAsync(
            t => t.Id == id,
            Builders<TaskTemplate>.Update
                .Set(t => t.Active, false)
                .Set(t => t.DeletedAt, DateTime.UtcNow));

    public Task ActivateAsync(ObjectId id) =>
        _context.TaskTemplates.UpdateOneAsync(
            t => t.Id == id,
            Builders<TaskTemplate>.Update
                .Set(t => t.Active, true)
                .Set(t => t.DeletedAt, (DateTime?)null)
                .Set(t => t.UpdatedAt, DateTime.UtcNow));
}
