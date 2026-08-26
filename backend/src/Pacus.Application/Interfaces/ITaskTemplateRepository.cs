using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITaskTemplateRepository
{
    Task<List<TaskTemplate>> GetActiveByUserAsync(ObjectId userId);
    Task<TaskTemplate?> GetByIdAsync(ObjectId id);
    Task<TaskTemplate> CreateAsync(TaskTemplate template);
    Task UpdateAsync(TaskTemplate template);
    Task SoftDeleteAsync(ObjectId id);

    // "Promove" uma tarefa que foi criada so para um dia a regra permanente — e o
    // caminho de replicar os dados da tarefa (ja guardados no template inativo desde
    // a criacao) para todos os dias seguintes, sem reescrever nada.
    Task ActivateAsync(ObjectId id);
}
