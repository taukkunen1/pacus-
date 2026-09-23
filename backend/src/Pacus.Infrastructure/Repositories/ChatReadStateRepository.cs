using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class ChatReadStateRepository : IChatReadStateRepository
{
    private readonly MongoDbContext _context;

    public ChatReadStateRepository(MongoDbContext context) => _context = context;

    public Task<ChatReadState?> GetAsync(ObjectId familyId, ObjectId userId) =>
        _context.ChatReadStates
            .Find(s => s.FamilyId == familyId && s.UserId == userId)
            .FirstOrDefaultAsync();

    public async Task UpsertAsync(
        ObjectId familyId,
        ObjectId userId,
        ObjectId lastReadMessageId,
        DateTime lastReadAt)
    {
        // User.Id e globalmente unico no PACUS, entao usa-lo como _id do estado
        // de leitura nos da unicidade nativa do Mongo sem depender de um indice
        // secundario aplicado fora da aplicacao. O fallback para um Id existente
        // preserva compatibilidade caso ja exista um documento criado pela versao
        // anterior com _id aleatorio.
        var existing = await GetAsync(familyId, userId);
        var stateId = existing?.Id ?? userId;

        var filter =
            Builders<ChatReadState>.Filter.Eq(s => s.Id, stateId) &
            Builders<ChatReadState>.Filter.Eq(s => s.FamilyId, familyId) &
            Builders<ChatReadState>.Filter.Eq(s => s.UserId, userId);

        var update = Builders<ChatReadState>.Update
            .SetOnInsert(s => s.Id, stateId)
            .SetOnInsert(s => s.FamilyId, familyId)
            .SetOnInsert(s => s.UserId, userId)
            .Max(s => s.LastReadMessageId, lastReadMessageId)
            .Max(s => s.LastReadAt, lastReadAt)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        try
        {
            await _context.ChatReadStates.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true });
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Duas primeiras leituras simultaneas podem ambas observar "sem estado".
            // Como ambas tentam inserir o mesmo _id=userId, o Mongo aceita apenas
            // uma; a outra repete como update e preserva o maior cursor via $max.
            await _context.ChatReadStates.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = false });
        }
    }

    public Task<List<ChatReadState>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatReadStates
            .Find(s => s.FamilyId == familyId)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.ChatReadStates.DeleteManyAsync(s => s.FamilyId == familyId);
}
