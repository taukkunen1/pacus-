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

    public async Task<TaskTemplate> CreateAsync(
        ObjectId familyId,
        ObjectId createdBy,
        CreateTaskRequest request)
    {
        var (type, period) = ParseTypeAndPeriod(request);

        var existing = await _taskTemplateRepository.GetActiveByUserAsync(familyId);

        var template = new TaskTemplate
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Period = period,
            Points = request.Points,
            Order = existing.Count + 1,
            Active = true,
            Recurrence = "daily",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await _taskTemplateRepository.CreateAsync(template);
    }

    public async Task<TaskTemplate> UpdateAsync(
        ObjectId familyId,
        string id,
        CreateTaskRequest request)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        var (type, period) = ParseTypeAndPeriod(request);

        template.Title = request.Title;
        template.Description = request.Description;
        template.Type = type;
        template.Period = period;
        template.Points = request.Points;
        template.UpdatedAt = DateTime.UtcNow;

        await _taskTemplateRepository.UpdateAsync(template);

        return template;
    }

    public async Task ActivateAsync(
        ObjectId familyId,
        string id)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        await _taskTemplateRepository.ActivateAsync(templateId);
    }

    public async Task DeleteAsync(
        ObjectId familyId,
        string id)
    {
        if (!ObjectId.TryParse(id, out var templateId))
            throw new InvalidOperationException("Id de tarefa invalido.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);

        // Mesma checagem de posse usada em Update/Activate: sem isso, qualquer adulto
        // autenticado (independente da familia) poderia excluir o template de outra
        // familia so sabendo (ou adivinhando) o ObjectId. Achado em auditoria de
        // seguranca (isolamento por FamilyId) — nunca chamar o repositorio direto
        // a partir do controller para operacoes que envolvem posse.
        if (template is null || template.FamilyId != familyId)
            throw new InvalidOperationException("Tarefa permanente nao encontrada.");

        await _taskTemplateRepository.SoftDeleteAsync(templateId);
    }

    private static (TaskType Type, TaskPeriod Period) ParseTypeAndPeriod(
        CreateTaskRequest request)
    {
        if (!Enum.TryParse<TaskType>(
                request.Type,
                ignoreCase: true,
                out var type))
        {
            throw new InvalidOperationException(
                $"Tipo de tarefa invalido: {request.Type}");
        }

        if (!Enum.TryParse<TaskPeriod>(
                request.Period,
                ignoreCase: true,
                out var period))
        {
            throw new InvalidOperationException(
                $"Periodo invalido: {request.Period}");
        }

        if (request.Points == 0 || request.Points < -10 || request.Points > 10)
        {
            throw new InvalidOperationException(
                "Cada tarefa deve valer entre 1 e 10 Pacus Points, ou entre -1 e -10 (penalidade). Zero nao e permitido.");
        }

        return (type, period);
    }
}