using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class TaskTemplateService : ITaskTemplateService
{
    private readonly ITaskTemplateRepository _taskTemplateRepository;

    public TaskTemplateService(ITaskTemplateRepository taskTemplateRepository) =>
        _taskTemplateRepository = taskTemplateRepository;

    public async Task<TaskTemplate> CreateAsync(ObjectId familyId, ObjectId createdBy, CreateTaskRequest request)
    {
        var (type, period) = ParseTypeAndPeriod(request);

        var existing = await _taskTemplateRepository.GetActiveByUserAsync(familyId);
        var template = new TaskTemplate
        {
            Id = ObjectId.GenerateNewId(),
            UserId = familyId,
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Period = period,
            Points = request.Points,
            Order = existing.Count + 1,
            Active = true, // criado direto pelo adulto — ja nasce ativo, ao contrario do ad-hoc.
            Recurrence = "daily",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await _taskTemplateRepository.CreateAsync(template);
    }

    public async Task<TaskTemplate> UpdateAsync(ObjectId familyId, string id, CreateTaskRequest request)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);
        if (template is null || template.UserId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        var (type, period) = ParseTypeAndPeriod(request);

        // Alterar o template NUNCA reescreve dias ja gerados — as copias em daily_routines.tasks
        // sao independentes por design (regra fundamental #10 da spec).
        template.Title = request.Title;
        template.Description = request.Description;
        template.Type = type;
        template.Period = period;
        template.Points = request.Points;
        template.UpdatedAt = DateTime.UtcNow;

        await _taskTemplateRepository.UpdateAsync(template);
        return template;
    }

    private static (TaskType Type, TaskPeriod Period) ParseTypeAndPeriod(CreateTaskRequest request)
    {
        if (!Enum.TryParse<TaskType>(request.Type, ignoreCase: true, out var type))
            throw new InvalidOperationException($"Tipo de tarefa invalido: {request.Type}");
        if (!Enum.TryParse<TaskPeriod>(request.Period, ignoreCase: true, out var period))
            throw new InvalidOperationException($"Periodo invalido: {request.Period}");
        return (type, period);
    }
}
