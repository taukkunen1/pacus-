using MongoDB.Bson;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IDailyRoutineRepository
{
    Task<DailyRoutine?> GetByUserAndDateAsync(ObjectId userId, string date);
    Task<DailyRoutine> CreateAsync(DailyRoutine routine);
    Task UpdateAsync(DailyRoutine routine);
    // Paginado (achado #4 da auditoria de API de 2026-09-01 -- ver docs/ESTADO_ATUAL.md).
    Task<(List<DailyRoutine> Items, long TotalCount)> GetHistoryAsync(ObjectId userId, string? from, string? to, int page, int pageSize);

    // Todas as rotinas (abertas e fechadas, sem filtro de data), para exportacao de dados (B2).
    Task<List<DailyRoutine>> GetAllByFamilyAsync(ObjectId familyId);

    // A qualquer momento so deve existir no maximo uma rotina com status Open por usuario.
    // Usado pelo fechamento do dia para achar o que precisa ser fechado (pode estar atrasado varios dias).
    Task<DailyRoutine?> GetLatestOpenAsync(ObjectId userId);

    // Remove todas as rotinas da familia -- exclusao de conta (LGPD, item B3).
    Task DeleteAllByFamilyAsync(ObjectId familyId);
}
