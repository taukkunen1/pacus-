using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[RequireRole(UserRole.Adult)]
[Route("api/v1/activity")]
public class ActivityController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly IPointTransactionRepository _points;
    private readonly IPacusGrowthRepository _growth;
    private readonly ICurrentUserService _currentUser;

    public ActivityController(
        IAuditLogRepository auditLogs,
        IPointTransactionRepository points,
        IPacusGrowthRepository growth,
        ICurrentUserService currentUser)
    {
        _auditLogs = auditLogs;
        _points = points;
        _growth = growth;
        _currentUser = currentUser;
    }

    // Timeline administrativa unificada. Restrita ao adulto porque agrega historico
    // comportamental, financeiro e de evolucao da familia em uma unica resposta.
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);
        var familyId = _currentUser.FamilyId;

        var auditTask = _auditLogs.GetAllByFamilyAsync(familyId);
        var pointsTask = _points.GetAllByFamilyAsync(familyId);
        var growthTask = _growth.GetAllByFamilyAsync(familyId);
        await Task.WhenAll(auditTask, pointsTask, growthTask);

        var items = new List<object>();

        items.AddRange(auditTask.Result.Select(a => (object)new
        {
            at = a.CreatedAt,
            kind = "audit",
            action = a.Action,
            title = a.EntityType,
            details = a.Details,
            actorRole = a.ActorRole.ToString(),
            delta = (int?)null
        }));

        items.AddRange(pointsTask.Result.Select(p => (object)new
        {
            at = p.CreatedAt,
            kind = "points",
            action = p.Type.ToString(),
            title = p.TaskTitle,
            details = p.Reason,
            actorRole = p.ActorRole.ToString(),
            delta = (int?)p.Points
        }));

        items.AddRange(growthTask.Result.Select(g => (object)new
        {
            at = g.CreatedAt,
            kind = "growth",
            action = "pacus.growth",
            title = $"{g.StageBefore} → {g.StageAfter}",
            details = $"Dia {g.Date}; tamanho {g.SizeBefore:0.##} → {g.SizeAfter:0.##}",
            actorRole = "System",
            delta = (int?)null
        }));

        // Tipos anonimos diferentes foram projetados acima para uma forma comum via
        // serializacao; ordenar antes de materializar exige uma chave explicita.
        var ordered = items
            .Select(x => new { Value = x, At = (DateTime)x.GetType().GetProperty("at")!.GetValue(x)! })
            .OrderByDescending(x => x.At)
            .Take(limit)
            .Select(x => x.Value)
            .ToList();

        return Ok(ordered);
    }
}
