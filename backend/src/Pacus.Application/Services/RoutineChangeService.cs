using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Exceptions;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class RoutineChangeService : IRoutineChangeService
{
    private readonly IRoutineChangeProposalRepository _proposalRepository;
    private readonly ITaskTemplateRepository _taskTemplateRepository;
    private readonly IDailyRoutineRepository _dailyRoutineRepository;

    public RoutineChangeService(
        IRoutineChangeProposalRepository proposalRepository,
        ITaskTemplateRepository taskTemplateRepository,
        IDailyRoutineRepository dailyRoutineRepository)
    {
        _proposalRepository = proposalRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _dailyRoutineRepository = dailyRoutineRepository;
    }

    public async Task<RoutineChangeProposal> CreateProposalAsync(
        ObjectId familyId,
        ObjectId requesterId,
        string requesterRole,
        CreateRoutineChangeProposalRequest request)
    {
        if (!requesterRole.Equals("child", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Este fluxo e para propostas do membro.");

        if (!ObjectId.TryParse(request.TaskTemplateId, out var templateId))
            throw new ValidationException("Tarefa permanente invalida.");

        var template = await _taskTemplateRepository.GetByIdAsync(templateId);
        if (template is null || template.FamilyId != familyId || template.DeletedAt is not null)
            throw new NotFoundException("Tarefa permanente nao encontrada.");

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not ("update" or "remove"))
            throw new ValidationException("Acao invalida. Use update ou remove.");

        string? proposedTitle = null;
        string? proposedDescription = null;
        string? proposedPeriod = null;

        if (action == "update")
        {
            proposedTitle = string.IsNullOrWhiteSpace(request.ProposedTitle)
                ? template.Title
                : request.ProposedTitle.Trim();
            TaskValidation.ValidateTitle(proposedTitle);

            proposedDescription = request.ProposedDescription ?? template.Description;
            TaskValidation.ValidateDescription(proposedDescription);

            proposedPeriod = string.IsNullOrWhiteSpace(request.ProposedPeriod)
                ? template.Period.ToString()
                : request.ProposedPeriod.Trim();

            if (!Enum.TryParse<TaskPeriod>(proposedPeriod, true, out _))
                throw new ValidationException("Periodo proposto invalido.");
        }

        var existing = await _proposalRepository.GetByRequesterAsync(familyId, requesterId);
        if (existing.Any(p => p.Status == "pending" && p.TaskTemplateId == templateId))
            throw new ValidationException("Ja existe uma proposta pendente para esta tarefa.");

        var now = DateTime.UtcNow;
        var proposal = new RoutineChangeProposal
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = familyId,
            RequestedBy = requesterId,
            TaskTemplateId = templateId,
            Action = action,
            CurrentTitle = template.Title,
            CurrentDescription = template.Description,
            CurrentPeriod = template.Period.ToString(),
            ProposedTitle = proposedTitle,
            ProposedDescription = proposedDescription,
            ProposedPeriod = proposedPeriod,
            MemberReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now,
        };

        return await _proposalRepository.CreateAsync(proposal);
    }

    public Task<List<RoutineChangeProposal>> GetMineAsync(ObjectId familyId, ObjectId requesterId) =>
        _proposalRepository.GetByRequesterAsync(familyId, requesterId);

    public Task<List<RoutineChangeProposal>> GetPendingAsync(ObjectId familyId) =>
        _proposalRepository.GetPendingByFamilyAsync(familyId);

    public async Task<RoutineChangeProposal> ApproveAsync(
        ObjectId familyId,
        string proposalId,
        ObjectId reviewerId,
        string? note)
    {
        var proposal = await GetPendingOwnedAsync(familyId, proposalId);
        var template = await _taskTemplateRepository.GetByIdAsync(proposal.TaskTemplateId);

        if (template is null || template.FamilyId != familyId || template.DeletedAt is not null)
            throw new NotFoundException("A tarefa permanente desta proposta nao existe mais.");

        if (proposal.Action == "remove")
        {
            await _taskTemplateRepository.SoftDeleteAsync(template.Id);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(proposal.ProposedTitle))
                template.Title = proposal.ProposedTitle.Trim();

            template.Description = proposal.ProposedDescription;

            if (!string.IsNullOrWhiteSpace(proposal.ProposedPeriod) &&
                Enum.TryParse<TaskPeriod>(proposal.ProposedPeriod, true, out var period))
                template.Period = period;

            template.UpdatedAt = DateTime.UtcNow;
            await _taskTemplateRepository.UpdateAsync(template);
        }

        // Se "Meu Amanhã" ja materializou o template, sincroniza apenas rotinas
        // planejadas. O dia atual permanece como fotografia do combinado vigente.
        var routines = await _dailyRoutineRepository.GetAllByFamilyAsync(familyId);
        foreach (var routine in routines.Where(r => r.Status == RoutineStatus.Planned))
        {
            var task = routine.Tasks.FirstOrDefault(t =>
                t.TaskTemplateId == template.Id.ToString() && t.DeletedAt is null);
            if (task is null) continue;

            if (proposal.Action == "remove")
            {
                task.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                task.Title = template.Title;
                task.Description = template.Description;
                task.Period = template.Period;
                task.UpdatedAt = DateTime.UtcNow;
            }

            routine.TomorrowPlanConfirmedAt = null;
            await _dailyRoutineRepository.UpdateAsync(routine);
        }

        proposal.Status = "approved";
        proposal.ReviewedBy = reviewerId;
        proposal.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _proposalRepository.UpdateAsync(proposal);
        return proposal;
    }

    public async Task<RoutineChangeProposal> RejectAsync(
        ObjectId familyId,
        string proposalId,
        ObjectId reviewerId,
        string? note)
    {
        var proposal = await GetPendingOwnedAsync(familyId, proposalId);
        proposal.Status = "rejected";
        proposal.ReviewedBy = reviewerId;
        proposal.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _proposalRepository.UpdateAsync(proposal);
        return proposal;
    }

    public async Task<List<RoutineSuggestionResponse>> GetSuggestionsAsync(ObjectId familyId)
    {
        var routines = (await _dailyRoutineRepository.GetAllByFamilyAsync(familyId))
            .Where(r => r.Status != RoutineStatus.Planned)
            .OrderByDescending(r => r.Date)
            .Take(30)
            .ToList();

        var suggestions = new List<RoutineSuggestionResponse>();
        if (routines.Count == 0) return suggestions;

        var allTasks = routines.SelectMany(r => r.Tasks)
            .Where(t => t.DeletedAt is null)
            .ToList();

        var memberTasks = allTasks.Where(t => t.CreatedByMember).ToList();
        var preferredPeriod = memberTasks
            .GroupBy(t => t.Period)
            .OrderByDescending(g => g.Count())
            .Select(g => (TaskPeriod?)g.Key)
            .FirstOrDefault();

        var repeatedOwnIdea = memberTasks
            .GroupBy(t => t.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (repeatedOwnIdea is not null)
        {
            var title = repeatedOwnIdea.First().Title;
            suggestions.Add(new RoutineSuggestionResponse(
                "repeat-own-" + title.ToLowerInvariant().Replace(' ', '-'),
                "own-idea",
                "Uma ideia que voce costuma escolher",
                $"Voce ja escolheu \"{title}\" mais de uma vez. Quer colocar isso no seu proximo plano?",
                SuggestedTitle: title));
        }

        var templateGroups = allTasks
            .Where(t => !string.IsNullOrEmpty(t.TaskTemplateId))
            .GroupBy(t => t.TaskTemplateId!)
            .Where(g => g.Count() >= 3)
            .Select(g => new
            {
                TemplateId = g.Key,
                Title = g.Last().Title,
                Period = g.Last().Period,
                Completion = g.Count(t => t.Status == TaskItemStatus.Done) / (double)g.Count(),
            })
            .OrderBy(x => x.Completion)
            .ToList();

        var difficult = templateGroups.FirstOrDefault(x => x.Completion < 0.60);
        if (difficult is not null && preferredPeriod is not null && preferredPeriod != difficult.Period)
        {
            suggestions.Add(new RoutineSuggestionResponse(
                "move-" + difficult.TemplateId,
                "routine-change",
                "Talvez outro periodo funcione melhor",
                $"\"{difficult.Title}\" tem sido dificil de concluir. Voce costuma escolher mais coisas para {PeriodLabel(preferredPeriod.Value)}. Quer propor essa mudanca?",
                difficult.TemplateId,
                preferredPeriod.Value.ToString()));
        }

        var selfStarted = allTasks
            .Where(t => t.Initiative == TaskInitiativeLevel.SelfStarted)
            .GroupBy(t => t.Period)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (selfStarted is not null)
        {
            suggestions.Add(new RoutineSuggestionResponse(
                "self-start-period-" + selfStarted.Key.ToString().ToLowerInvariant(),
                "insight",
                "Seu horario mais independente",
                $"Nos ultimos dias, voce comecou mais tarefas por conta propria {PeriodLabel(selfStarted.Key)}."));
        }

        return suggestions.Take(4).ToList();
    }

    private async Task<RoutineChangeProposal> GetPendingOwnedAsync(ObjectId familyId, string proposalId)
    {
        if (!ObjectId.TryParse(proposalId, out var id))
            throw new ValidationException("Proposta invalida.");

        var proposal = await _proposalRepository.GetByIdAsync(id);
        if (proposal is null || proposal.FamilyId != familyId)
            throw new NotFoundException("Proposta nao encontrada.");
        if (proposal.Status != "pending")
            throw new ValidationException("Esta proposta ja foi revisada.");
        return proposal;
    }

    private static string PeriodLabel(TaskPeriod period) => period switch
    {
        TaskPeriod.Morning => "de manha",
        TaskPeriod.Afternoon => "a tarde",
        TaskPeriod.Evening => "a noite",
        _ => "em outro horario",
    };
}
