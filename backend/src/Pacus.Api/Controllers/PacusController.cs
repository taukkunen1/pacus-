using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
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

    // Leitura liberada para os dois papeis â€” a crianÃ§a tambem acompanha o PACUS crescer.
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var pacus = await _pacusRepository.GetByFamilyIdAsync(_currentUser.FamilyId);
        return pacus is null ? NotFound() : Ok(pacus);
    }

    [HttpGet("me/state")]
    public IActionResult GetState() => Ok(new { state = "idle" });

    // Configuracao de comportamento/habitat e administrativa â€” "Painel Adulto: configurar PACUS".
    [RequireRole(UserRole.Adult)]
    [HttpPut("me/state")]
    public IActionResult UpdateState() => NoContent();
}

