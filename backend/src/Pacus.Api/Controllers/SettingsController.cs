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
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(
        ISettingsRepository settingsRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser)
    {
        _settingsRepository = settingsRepository;
        _userRepository = userRepository;
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

    // Valor de referencia de 1 PP em reais. A leitura e autenticada e a alteracao
    // fica restrita ao adulto responsavel pela familia.
    [HttpGet("point-value")]
    public async Task<IActionResult> GetPointValue()
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId);
        var rate = settings?.PointToBrlRate ?? Settings.DefaultPointToBrlRate;

        // Cura somente o default historico nunca escolhido pelo adulto. Depois que
        // PointToBrlRateConfigured=true, R$ 0,05 passa a ser uma escolha legitima.
        if (settings is not null
            && !settings.PointToBrlRateConfigured
            && Math.Abs(settings.PointToBrlRate - Settings.LegacyDefaultPointToBrlRate) < 0.000001)
        {
            settings.PointToBrlRate = Settings.DefaultPointToBrlRate;
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.UpsertAsync(settings);
            rate = settings.PointToBrlRate;
        }

        return Ok(new { rate });
    }

    [RequireRole(UserRole.Adult)]
    [HttpPut("point-value")]
    public async Task<IActionResult> UpdatePointValue([FromBody] UpdatePointToBrlRateRequest request)
    {
        if (double.IsNaN(request.Rate)
            || double.IsInfinity(request.Rate)
            || request.Rate < 0.01
            || request.Rate > 100)
        {
            return BadRequest(new
            {
                error = "O valor de 1 PP deve estar entre R$ 0,01 e R$ 100,00."
            });
        }

        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId)
            ?? new Settings
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                CreatedAt = DateTime.UtcNow,
            };

        settings.PointToBrlRate = Math.Round(request.Rate, 2, MidpointRounding.AwayFromZero);
        settings.PointToBrlRateConfigured = true;
        settings.UpdatedAt = DateTime.UtcNow;

        await _settingsRepository.UpsertAsync(settings);

        return Ok(new { rate = settings.PointToBrlRate });
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
    [HttpGet("water")]
    public async Task<IActionResult> GetWater()
    {
        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId);
        var familyMembers = await _userRepository.GetByFamilyAsync(_currentUser.FamilyId);
        var visibleProfiles = _currentUser.Role == UserRole.Adult
            ? familyMembers
            : familyMembers.Where(member => member.Id == _currentUser.UserId).ToList();

        var profiles = visibleProfiles.Select(member =>
        {
            var profile = settings?.GetWaterProfile(member.Id) ?? new WaterProfileSettings();
            return new
            {
                profileId = member.Id.ToString(),
                name = member.Name,
                role = member.Role.ToString().ToLowerInvariant(),
                goalMl = profile.GoalMl,
                rewardPoints = profile.RewardPoints,
            };
        }).ToList();

        var current = settings?.GetWaterProfile(_currentUser.UserId) ?? new WaterProfileSettings();
        return Ok(new
        {
            profileId = _currentUser.UserId.ToString(),
            goalMl = current.GoalMl,
            rewardPoints = current.RewardPoints,
            profiles,
        });
    }

    [RequireRole(UserRole.Adult)]
    [HttpPut("water")]
    public async Task<IActionResult> UpdateWater([FromBody] UpdateWaterGoalRequest request)
    {
        if (request.GoalMl < 500 || request.GoalMl > 4000)
            return BadRequest(new { error = "A meta configurada deve estar entre 500 e 4000 mL." });
        if (request.RewardPoints < 0 || request.RewardPoints > 5)
            return BadRequest(new { error = "A recompensa de hidratação deve estar entre 0 e 5 PP." });

        var targetProfileId = _currentUser.UserId;
        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            if (!ObjectId.TryParse(request.ProfileId, out targetProfileId))
                return BadRequest(new { error = "Perfil de hidratação inválido." });
        }

        var familyMembers = await _userRepository.GetByFamilyAsync(_currentUser.FamilyId);
        if (!familyMembers.Any(member => member.Id == targetProfileId))
            return NotFound(new { error = "Perfil não encontrado nesta família." });

        var settings = await _settingsRepository.GetByUserIdAsync(_currentUser.FamilyId)
            ?? new Settings
            {
                Id = ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                CreatedAt = DateTime.UtcNow,
            };

        settings.WaterProfiles[targetProfileId.ToString()] = new WaterProfileSettings
        {
            GoalMl = request.GoalMl,
            RewardPoints = request.RewardPoints,
        };
        settings.UpdatedAt = DateTime.UtcNow;
        await _settingsRepository.UpsertAsync(settings);
        return Ok(new
        {
            profileId = targetProfileId.ToString(),
            goalMl = request.GoalMl,
            rewardPoints = request.RewardPoints,
        });
    }
}
