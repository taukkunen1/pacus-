using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITaskTemplateService
{
    Task<TaskTemplate> CreateAsync(
        ObjectId familyId,
        ObjectId createdBy,
        CreateTaskRequest request);

    Task<TaskTemplate> UpdateAsync(
        ObjectId familyId,
        string id,
        CreateTaskRequest request);

    Task ActivateAsync(
        ObjectId familyId,
        string id);

    Task DeleteAsync(
        ObjectId familyId,
        string id,
        ObjectId actorId,
        string actorRole);
}
