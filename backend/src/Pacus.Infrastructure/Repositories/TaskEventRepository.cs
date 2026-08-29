using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class TaskEventRepository : ITaskEventRepository
{
    private readonly MongoDbContext _context;

    public TaskEventRepository(MongoDbContext context) => _context = context;

    public async Task<TaskEvent> CreateAsync(TaskEvent taskEvent)
    {
        await _context.TaskEvents.InsertOneAsync(taskEvent);
        return taskEvent;
    }

    public Task<List<TaskEvent>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.TaskEvents.Find(e => e.UserId == familyId)
            .SortByDescending(e => e.CreatedAt)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.TaskEvents.DeleteManyAsync(e => e.UserId == familyId);
}
