using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IRoutineChangeService
{
    Task<RoutineChangeProposal> CreateProposalAsync(
        ObjectId familyId,
        ObjectId requesterId,
        string requesterRole,
        CreateRoutineChangeProposalRequest request);

    Task<List<RoutineChangeProposal>> GetMineAsync(ObjectId familyId, ObjectId requesterId);
    Task<List<RoutineChangeProposal>> GetPendingAsync(ObjectId familyId);
    Task<RoutineChangeProposal> ApproveAsync(ObjectId familyId, string proposalId, ObjectId reviewerId, string? note);
    Task<RoutineChangeProposal> RejectAsync(ObjectId familyId, string proposalId, ObjectId reviewerId, string? note);
    Task<List<RoutineSuggestionResponse>> GetSuggestionsAsync(ObjectId familyId);
}
