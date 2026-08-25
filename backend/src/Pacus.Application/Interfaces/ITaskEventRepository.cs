using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITaskEventRepository
{
    Task<TaskEvent> CreateAsync(TaskEvent taskEvent);
}
