using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;

namespace Pacus.Api.Controllers;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md), item 6: "feedback
// de autonomia" -- sem try/catch aqui de proposito, mesmo padrao dos outros
// controllers (ver Pacus.Api.Middleware.AppExceptionHandler e achado #1 da
// auditoria de API de 2026-09-01).
[ApiController]
[Authorize]
[Route("api/v1/autonomy")]
public class AutonomyController : ControllerBase
{
    private readonly IAutonomyService _autonomyService;
    private readonly IFamilyTimezoneService _familyTimezoneService;
    private readonly ICurrentUserService _currentUser;

    public AutonomyController(
        IAutonomyService autonomyService,
        IFamilyTimezoneService familyTimezoneService,
        ICurrentUserService currentUser)
    {
        _autonomyService = autonomyService;
        _familyTimezoneService = familyTimezoneService;
        _currentUser = currentUser;
    }

    [HttpGet("weekly")]
    public async Task<IActionResult> GetWeekly()
    {
        var familyId = _currentUser.FamilyId;
        var timezone = await _familyTimezoneService.GetTimezoneAsync(familyId);
        var report = await _autonomyService.GetWeeklyReportAsync(familyId, timezone);
        return Ok(report);
    }
}

