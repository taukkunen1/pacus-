using MongoDB.Bson;
using MongoDB.Driver;
using Pacus.Application.Exceptions;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Infrastructure.Mongo;

namespace Pacus.Infrastructure.Repositories;

public class DailyRoutineRepository : IDailyRoutineRepository
{
    private readonly MongoDbContext _context;

    public DailyRoutineRepository(MongoDbContext context) => _context = context;

    public Task<DailyRoutine?> GetByUserAndDateAsync(ObjectId userId, string date) =>
        _context.DailyRoutines.Find(r => r.FamilyId == userId && r.Date == date).FirstOrDefaultAsync();

    public async Task<DailyRoutine> CreateAsync(DailyRoutine routine)
    {
        await _context.DailyRoutines.InsertOneAsync(routine);
        return routine;
    }

    // Concorrencia otimista (achado #5 da auditoria de API de 2026-09-01 -- ver
    // docs/ESTADO_ATUAL.md): antes era um ReplaceOneAsync filtrado so por Id, sem guarda
    // de versao -- duas requisicoes lendo a mesma rotina (ex.: crianca completando uma
    // tarefa e adulto ajustando o game timer quase ao mesmo tempo) faziam um "lost
    // update" silencioso, a segunda gravacao sobrescrevendo a primeira sem erro nenhum.
    // Agora o filtro exige Version == a versao que foi lida; se outra escrita ja
    // aconteceu no meio do caminho, o filtro nao bate em nada (MatchedCount == 0) e a
    // gravacao falha alto (ConflictException, 409) em vez de silenciosamente perder a
    // mudanca de alguem. O chamador que perdeu a corrida simplesmente tenta de novo (o
    // service nao faz retry automatico aqui -- ver nota no achado #5 em
    // docs/ESTADO_ATUAL.md sobre esse trade-off).
    //
    // Bug encontrado em producao (2026-09-01, apos o deploy do achado #5): rotinas
    // criadas ANTES do campo Version existir nao tem "version" gravado no documento do
    // Mongo. Version desserializa como 0 em C# (comentario em DailyRoutine.cs), mas uma
    // query { version: 0 } NAO bate em documento sem o campo -- o Mongo nao trata "campo
    // ausente" como igual a 0 numa comparacao direta. Resultado: TODO UpdateAsync numa
    // rotina legada falhava com ConflictException, sempre, mesmo sem nenhuma concorrencia
    // real (nao era uma corrida passageira -- por isso o retry em GetToday nao resolvia).
    // Corrigido tratando "campo ausente" como equivalente a versao 0 no filtro, so para
    // esse caso -- sem precisar de script de migracao nem tocar nos documentos existentes.
    public async Task UpdateAsync(DailyRoutine routine)
    {
        var expectedVersion = routine.Version;
        routine.Version = expectedVersion + 1;

        var idFilter = Builders<DailyRoutine>.Filter.Eq(r => r.Id, routine.Id);
        var versionFilter = expectedVersion == 0
            ? Builders<DailyRoutine>.Filter.Or(
                Builders<DailyRoutine>.Filter.Eq(r => r.Version, 0),
                Builders<DailyRoutine>.Filter.Exists(r => r.Version, false))
            : Builders<DailyRoutine>.Filter.Eq(r => r.Version, expectedVersion);

        var result = await _context.DailyRoutines.ReplaceOneAsync(
            idFilter & versionFilter,
            routine);

        if (result.MatchedCount == 0)
        {
            // Reverte a mutacao local do numero de versao -- o chamador pode querer
            // inspecionar/logar o objeto depois do throw, e ele nao foi de fato salvo.
            routine.Version = expectedVersion;

            throw new ConflictException(
                "Esta rotina foi alterada por outra requisicao enquanto isso era processado. Tente novamente.");
        }
    }

    // Paginado (achado #4 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md):
    // antes devolvia a lista inteira de dias encerrados sem limite, que so cresce com o
    // tempo (um documento a mais por dia). TotalCount vem de uma segunda query
    // (CountDocumentsAsync) -- o Mongo nao devolve isso de graca junto com Skip/Limit.
    public async Task<(List<DailyRoutine> Items, long TotalCount)> GetHistoryAsync(
        ObjectId userId, string? from, string? to, int page, int pageSize)
    {
        var filterBuilder = Builders<DailyRoutine>.Filter;
        var filter = filterBuilder.Eq(r => r.FamilyId, userId) &
                     filterBuilder.Eq(r => r.Status, Domain.Enums.RoutineStatus.Closed);

        if (!string.IsNullOrEmpty(from))
            filter &= filterBuilder.Gte(r => r.Date, from);
        if (!string.IsNullOrEmpty(to))
            filter &= filterBuilder.Lte(r => r.Date, to);

        var totalCount = await _context.DailyRoutines.CountDocumentsAsync(filter);
        var items = await _context.DailyRoutines.Find(filter)
            .SortByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<DailyRoutine?> GetLatestOpenAsync(ObjectId userId) =>
        _context.DailyRoutines
            .Find(r => r.FamilyId == userId && r.Status == Domain.Enums.RoutineStatus.Open)
            .SortByDescending(r => r.Date)
            .FirstOrDefaultAsync();

    public Task<List<DailyRoutine>> GetAllByFamilyAsync(ObjectId familyId) =>
        _context.DailyRoutines.Find(r => r.FamilyId == familyId)
            .SortByDescending(r => r.Date)
            .ToListAsync();

    public Task DeleteAllByFamilyAsync(ObjectId familyId) =>
        _context.DailyRoutines.DeleteManyAsync(r => r.FamilyId == familyId);
}
