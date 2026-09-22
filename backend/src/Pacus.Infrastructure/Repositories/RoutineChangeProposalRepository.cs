using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class RoutineChangeProposalRepository : IRoutineChangeProposalRepository
{
    private readonly MongoDbContext _context;
    public RoutineChangeProposalRepository(MongoDbContext context) => _context = context;

    public async Task<RoutineChangeProposal> CreateAsync(RoutineChangeProposal proposal)
    {
        await _context.RoutineChangeProposals.InsertOneAsync(proposal);
        return proposal;
    }

    public Task<RoutineChangeProposal?> GetByIdAsync(ObjectId id) =>
        _context.RoutineChangeProposals.Find(x => x.Id == id).FirstOrDefaultAsync();

    public Task<List<RoutineChangeProposal>> GetPendingByFamilyAsync(ObjectId familyId) =>
        _context.RoutineChangeProposals
            .Find(x => x.FamilyId == familyId && x.Status == "pending")
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

    public Task<List<RoutineChangeProposal>> GetByRequesterAsync(ObjectId familyId, ObjectId requesterId) =>
        _context.RoutineChangeProposals
            .Find(x => x.FamilyId == familyId && x.RequestedBy == requesterId)
            .SortByDescending(x => x.CreatedAt)
            .Limit(50)
            .ToListAsync();

    public Task UpdateAsync(RoutineChangeProposal proposal) =>
        _context.RoutineChangeProposals.ReplaceOneAsync(x => x.Id == proposal.Id, proposal);

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.RoutineChangeProposals.DeleteManyAsync(x => x.FamilyId == familyId);
}
