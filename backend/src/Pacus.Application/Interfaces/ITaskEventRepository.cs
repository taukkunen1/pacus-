using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITaskEventRepository
{
    Task<TaskEvent> CreateAsync(TaskEvent taskEvent);

    // Todos os eventos de tarefa, para exportacao de dados (B2).
    Task<List<TaskEvent>> GetAllByFamilyAsync(ObjectId familyId);
}
