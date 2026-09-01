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

    // Calendario de estagios do PACUS (ex.: Egg 2026-08-26 -> Adult 2026-09-26), usado por
    // DayClosingService.DetermineStage. Antes so dava pra configurar direto no Mongo/API crua
    // (foi como corrigimos o estagio/tamanho manualmente); agora tem endpoint dedicado.
    // Leitura liberada pros dois papeis, igual o game-timer acima.
    [HttpGet("growth-stages")]
    public async Task<IActionResult> GetGrowthStages()
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId);
        var stages = (settings?.GrowthStages ?? new List<GrowthStageConfig>())
            .OrderBy(s => s.Date)
            .Select(s => new GrowthStageConfigDto(s.Stage.ToString(), s.Date));

        return Ok(stages);
    }

    // So o adulto configura. Substitui a lista inteira (mais simples e previsivel do que
    // um patch parcial) -- o frontend sempre manda o calendario completo de volta.
    [RequireRole(UserRole.Adult)]
    [HttpPut("growth-stages")]
    public async Task<IActionResult> UpdateGrowthStages([FromBody] UpdateGrowthStagesRequest request)
    {
        var parsed = new List<GrowthStageConfig>();
        foreach (var stage in request.Stages)
        {
            if (!Enum.TryParse<PacusStage>(stage.Stage, ignoreCase: true, out var parsedStage))
                return BadRequest(new { error = $"Estagio invalido: '{stage.Stage}'." });

            if (!DateTime.TryParseExact(stage.Date, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                return BadRequest(new { error = $"Data invalida (use AAAA-MM-DD): '{stage.Date}'." });

            parsed.Add(new GrowthStageConfig { Stage = parsedStage, Date = stage.Date });
        }

        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId)
            ?? new Settings
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                CreatedAt = DateTime.UtcNow,
            };

        settings.GrowthStages = parsed;
        settings.UpdatedAt = DateTime.UtcNow;
        await _settingsRepository.UpsertAsync(settings);

        var result = settings.GrowthStages.OrderBy(s => s.Date)
            .Select(s => new GrowthStageConfigDto(s.Stage.ToString(), s.Date));

        return Ok(result);
    }
}
