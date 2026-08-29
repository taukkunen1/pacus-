using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITaskEventRepository
{
    Task<TaskEvent> CreateAsync(TaskEvent taskEvent);

    // Todos os eventos de tarefa, para exportacao de dados (B2).
    Task<List<TaskEvent>> GetAllByFamilyAsync(ObjectId familyId);

    // Remove todos os eventos de tarefa da familia -- exclusao de conta (LGPD, item B3).
    // Nota: o campo se chama UserId mas guarda o FamilyId (nao foi renomeado no A4) -- ver docs/DATA_MAP.md.
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
