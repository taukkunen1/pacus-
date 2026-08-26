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
}
