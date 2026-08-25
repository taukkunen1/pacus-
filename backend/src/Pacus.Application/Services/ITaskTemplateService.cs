using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Services;

public interface ITaskTemplateService
{
    Task<TaskTemplate> CreateAsync(ObjectId familyId, ObjectId createdBy, CreateTaskRequest request);

    // Lanca InvalidOperationException se o template nao existe ou pertence a outra familia.
    Task<TaskTemplate> UpdateAsync(ObjectId familyId, string id, CreateTaskRequest request);
}
