using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/water")]
public class WaterController : ControllerBase
{
    private const int MaxEntryMl = 1000;
    private readonly IWaterIntakeRepository _water;
    private readonly ISettingsRepository _settings;
    private readonly IFamilyTimezoneService _timezone;
    private readonly ICurrentUserService _currentUser;
    private readonly IPointsService _pointsService;
    private readonly IPointTransactionRepository _pointTransactions;

    public WaterController(IWaterIntakeRepository water, ISettingsRepository settings,
        IFamilyTimezoneService timezone, ICurrentUserService currentUser,
        IPointsService pointsService, IPointTransactionRepository pointTransactions)
    {
        _water = water;
        _settings = settings;
        _timezone = timezone;
        _currentUser = currentUser;
        _pointsService = pointsService;
        _pointTransactions = pointTransactions;
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var timezone = await _timezone.GetTimezoneAsync(_currentUser.FamilyId);
        var date = TimezoneHelper.GetOperationalDate(timezone, DateTime.UtcNow);
        return Ok(await BuildDayAsync(date));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddWaterRequest request)
    {
        if (request.AmountMl < 50 || request.AmountMl > MaxEntryMl)
            return BadRequest(new { error = $"Registre entre 50 e {MaxEntryMl} mL por vez." });
        if (string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 100)
            return BadRequest(new { error = "eventId invalido." });

        var existing = await _water.GetByEventIdAsync(
            _currentUser.FamilyId, _currentUser.UserId, request.EventId.Trim());
        if (existing is not null)
        {
            var existingProfile = await GetWaterProfileAsync(_currentUser.UserId);
            var existingEvents = await _water.GetByDateAsync(
                _currentUser.FamilyId, existing.Date, _currentUser.UserId);
            if (existingProfile.RewardPoints > 0
                && existingEvents.Sum(x => x.AmountMl) >= existingProfile.GoalMl)
            {
                await EnsureRewardAsync(existing.Date, existingProfile.RewardPoints);
            }
            return Ok(await BuildDayAsync(existing.Date));
        }

        var timezone = await _timezone.GetTimezoneAsync(_currentUser.FamilyId);
        var date = TimezoneHelper.GetOperationalDate(timezone, DateTime.UtcNow);
        var profile = await GetWaterProfileAsync(_currentUser.UserId);
        var beforeEvents = await _water.GetByDateAsync(_currentUser.FamilyId, date, _currentUser.UserId);
        var beforeTotal = beforeEvents.Sum(x => x.AmountMl);

        var intake = new WaterIntake
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            ActorId = _currentUser.UserId,
            Date = date,
            AmountMl = request.AmountMl,
            EventId = request.EventId.Trim(),
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        var created = true;
        try { await _water.CreateAsync(intake); }
        catch (MongoDB.Driver.MongoWriteException ex) when (
            ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            // Retry/offline sync do mesmo evento: idempotente.
            created = false;
        }

        // Há no máximo uma recompensa por perfil/dia, e nenhuma recompensa adicional
        // por ultrapassar a meta. O registro de água continua sendo individual.
        if (created && profile.RewardPoints > 0
            && beforeTotal < profile.GoalMl
            && beforeTotal + request.AmountMl >= profile.GoalMl)
        {
            await EnsureRewardAsync(date, profile.RewardPoints);
        }

        return Ok(await BuildDayAsync(date));
    }

    [HttpDelete("today/latest")]
    public async Task<IActionResult> UndoLatest()
    {
        var timezone = await _timezone.GetTimezoneAsync(_currentUser.FamilyId);
        var date = TimezoneHelper.GetOperationalDate(timezone, DateTime.UtcNow);
        var latest = await _water.GetLatestByDateAsync(
            _currentUser.FamilyId, date, _currentUser.UserId);
        if (latest is not null)
            await _water.DeleteAsync(_currentUser.FamilyId, latest.Id, _currentUser.UserId);

        var profile = await GetWaterProfileAsync(_currentUser.UserId);
        var remaining = await _water.GetByDateAsync(_currentUser.FamilyId, date, _currentUser.UserId);
        if (remaining.Sum(x => x.AmountMl) < profile.GoalMl)
            await EnsureReversalAsync(date);

        return Ok(await BuildDayAsync(date));
    }

    private async Task<object> BuildDayAsync(string date)
    {
        var events = await _water.GetByDateAsync(_currentUser.FamilyId, date, _currentUser.UserId);
        var profile = await GetWaterProfileAsync(_currentUser.UserId);
        var total = events.Sum(x => x.AmountMl);
        var awardedPoints = await GetEffectiveRewardAsync(date);

        return new
        {
            date,
            profileId = _currentUser.UserId.ToString(),
            goalMl = profile.GoalMl,
            totalMl = total,
            rewardPoints = profile.RewardPoints,
            rewarded = awardedPoints > 0,
            task = new
            {
                id = WaterTaskId(date),
                title = "Beber água",
                points = profile.RewardPoints,
                isDone = total >= profile.GoalMl
            },
            entries = events.Select(x => new
            {
                id = x.Id.ToString(),
                amountMl = x.AmountMl,
                occurredAt = x.OccurredAt,
                actorId = x.ActorId.ToString()
            })
        };
    }

    private async Task<WaterProfileSettings> GetWaterProfileAsync(ObjectId profileId)
    {
        var settings = await _settings.GetByUserIdAsync(_currentUser.FamilyId);
        return settings?.GetWaterProfile(profileId) ?? new WaterProfileSettings();
    }

    private string WaterTaskId(string date) => $"water:{_currentUser.UserId}:{date}";

    private async Task<int> GetEffectiveRewardAsync(string date)
    {
        var taskId = WaterTaskId(date);
        var transactions = await _pointTransactions.GetAllByFamilyAsync(_currentUser.FamilyId);
        return transactions
            .Where(x => x.TaskId == taskId
                && (x.Type == PointTransactionType.Award || x.Type == PointTransactionType.Reversal))
            .Sum(x => x.Points);
    }

    private async Task EnsureRewardAsync(string date, int rewardPoints)
    {
        if (await GetEffectiveRewardAsync(date) > 0) return;

        await _pointsService.RecordAsync(
            _currentUser.FamilyId,
            null,
            date,
            WaterTaskId(date),
            "Meta de hidratação",
            PointTransactionType.Award,
            rewardPoints,
            _currentUser.UserId,
            _currentUser.Role,
            "Meta diária de hidratação do perfil concluída");
    }

    private async Task EnsureReversalAsync(string date)
    {
        var effectiveReward = await GetEffectiveRewardAsync(date);
        if (effectiveReward <= 0) return;

        await _pointsService.RecordAsync(
            _currentUser.FamilyId,
            null,
            date,
            WaterTaskId(date),
            "Meta de hidratação",
            PointTransactionType.Reversal,
            -effectiveReward,
            _currentUser.UserId,
            _currentUser.Role,
            "Registro desfeito; meta de hidratação do perfil incompleta");
    }

    public record AddWaterRequest(int AmountMl, string EventId);
}