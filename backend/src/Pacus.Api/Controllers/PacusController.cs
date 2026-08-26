using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pacus")]
public class PacusController : ControllerBase
{
    private readonly IPacusRepository _pacusRepository;
    private readonly ICurrentUserService _currentUser;

    public PacusController(IPacusRepository pacusRepository, ICurrentUserService currentUser)
    {
        _pacusRepository = pacusRepository;
        _currentUser = currentUser;
    }

    // Leitura liberada para os dois papeis - a crianca tambem acompanha o PACUS crescer.
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var pacus = await _pacusRepository.GetByFamilyIdAsync(_currentUser.FamilyId);
        return pacus is null ? NotFound() : Ok(pacus);
    }

    [HttpGet("me/state")]
    public async Task<IActionResult> GetState()
    {
        var pacus = await _pacusRepository.GetByFamilyIdAsync(_currentUser.FamilyId);
        if (pacus is null) return NotFound();

        return Ok(new
        {
            stage = pacus.Stage,
            size = pacus.Size,
            totalClosedDays = pacus.TotalClosedDays,
            lastGrowthDate = pacus.LastGrowthDate
        });
    }

    // Configuracao de comportamento/habitat e administrativa - "Painel Adulto: configurar PACUS".
    // Tambem usado para corrigir manualmente o estagio/tamanho quando a familia ja tinha
    // um PACUS em andamento antes deste app (migracao de progresso).
    [RequireRole(UserRole.Adult)]
    [HttpPut("me/state")]
    public async Task<IActionResult> UpdateState([FromBody] UpdatePacusStateRequest request)
    {
        var pacus = await _pacusRepository.GetByFamilyIdAsync(_currentUser.FamilyId);
        if (pacus is null) return NotFound();

        if (request.Stage is not null) pacus.Stage = request.Stage.Value;
        if (request.Size is not null) pacus.Size = request.Size.Value;
        if (request.TotalClosedDays is not null) pacus.TotalClosedDays = request.TotalClosedDays.Value;

        pacus.UpdatedAt = DateTime.UtcNow;
        await _pacusRepository.UpdateAsync(pacus);

        return Ok(pacus);
    }
}
