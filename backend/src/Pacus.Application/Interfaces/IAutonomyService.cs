using MongoDB.Bson;
using Pacus.Application.DTOs;

namespace Pacus.Application.Interfaces;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 6:
// "feedback de autonomia" -- evolucao de independencia, nao so quantidade de
// tarefas concluidas.
public interface IAutonomyService
{
    // utcNow e opcional (default = DateTime.UtcNow), so pra permitir teste
    // deterministico -- mesmo padrao de TimezoneHelper.GetOperationalDate.
    Task<AutonomyWeeklyReportResponse> GetWeeklyReportAsync(ObjectId familyId, string timezone, DateTime? utcNow = null);
}

