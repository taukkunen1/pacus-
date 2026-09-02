using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pacus.Application.DTOs;
using Pacus.Application.Services;

namespace Pacus.Api.Controllers;

[ApiController]
[Route("api/v1/bootstrap")]
[EnableRateLimiting("bootstrap")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;

    public BootstrapController(IBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
    }

    // Sem try/catch aqui de proposito: ConflictException ("ja existe um usuario
    // adulto com este email") vira 409 sozinha, via
    // Pacus.Api.Middleware.AppExceptionHandler (achado #1 da auditoria de API de
    // 2026-09-01 -- ver docs/ESTADO_ATUAL.md).
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateInitialFamily(
        [FromBody] BootstrapRequest request)
    {
        var result = await _bootstrapService.CreateInitialFamilyAsync(request);
        return Created("", result);
    }
}
