using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;
using MongoDB.Bson;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(ISettingsRepository settingsRepository, ICurrentUserService currentUser)
    {
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
    }

    // So devolve o que e relevante pro frontend hoje; leitura liberada pros dois
    // papeis (a crianca tambem pode ver se a trava de tempo de jogo esta ligada).
    [HttpGet("game-timer")]
    public async Task<IActionResult> GetGameTimer()
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId);
        return Ok(new
        {
            enabled = settings?.GameTimerEnabled ?? false,
            minutes = settings?.GameTimerMinutes ?? 120
        });
    }

    // Liga/desliga e ajusta a duracao. So o adulto decide isso — e desligado por
    // padrao para toda familia nova, entao so quem ativa aqui explicitamente
    // ganha essa mecanica.
    [RequireRole(UserRole.Adult)]
    [HttpPut("game-timer")]
    public async Task<IActionResult> UpdateGameTimer([FromBody] UpdateGameTimerRequest request)
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId)
            ?? new Settings
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                CreatedAt = DateTime.UtcNow,
            };

        settings.GameTimerEnabled = request.Enabled;
        if (request.Minutes is not null && request.Minutes > 0)
            settings.GameTimerMinutes = request.Minutes.Value;
        settings.UpdatedAt = DateTime.UtcNow;

        await _settingsRepository.UpsertAsync(settings);

        return Ok(new { settings.GameTimerEnabled, settings.GameTimerMinutes });
    }
}
