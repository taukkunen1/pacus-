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
    private sealed record TimelineItem(
        DateTime At, string Kind, string Action, string Title,
        string? Details, string ActorRole, int? Delta);
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

        var items = new List<TimelineItem>();

        items.AddRange(auditTask.Result.Select(a => new TimelineItem(
            a.CreatedAt, "audit", a.Action, a.EntityType, a.Details,
            a.ActorRole.ToString(), null)));

        items.AddRange(pointsTask.Result.Select(p => new TimelineItem(
            p.CreatedAt, "points", p.Type.ToString(), p.TaskTitle, p.Reason,
            p.ActorRole.ToString(), p.Points)));

        items.AddRange(growthTask.Result.Select(g => new TimelineItem(
            g.CreatedAt, "growth", "pacus.growth",
            $"{g.StageBefore} → {g.StageAfter}",
            $"Dia {g.Date}; tamanho {g.SizeBefore:0.##} → {g.SizeAfter:0.##}",
            "System", null)));

        var ordered = items
            .OrderByDescending(x => x.At)
            .Take(limit)
            .ToList();

        return Ok(ordered);
    }
}
