using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IRoutineChangeProposalRepository
{
    Task<RoutineChangeProposal> CreateAsync(RoutineChangeProposal proposal);
    Task<RoutineChangeProposal?> GetByIdAsync(ObjectId id);
    Task<List<RoutineChangeProposal>> GetPendingByFamilyAsync(ObjectId familyId);
    Task<List<RoutineChangeProposal>> GetByRequesterAsync(ObjectId familyId, ObjectId requesterId);
    Task UpdateAsync(RoutineChangeProposal proposal);
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
