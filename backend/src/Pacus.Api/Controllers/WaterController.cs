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
        if (request.AmountMl < 50 || request.AmountMl > 2000)
            return BadRequest(new { error = "Informe uma quantidade entre 50 e 2000 mL." });
        if (string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 100)
            return BadRequest(new { error = "eventId invalido." });

        var existing = await _water.GetByEventIdAsync(_currentUser.FamilyId, request.EventId);
        if (existing is not null)
            return Ok(await BuildDayAsync(existing.Date));

        var timezone = await _timezone.GetTimezoneAsync(_currentUser.FamilyId);
        var date = TimezoneHelper.GetOperationalDate(timezone, DateTime.UtcNow);
        var settings = await _settings.GetByUserIdAsync(_currentUser.FamilyId);
        var goal = settings?.WaterGoalMl ?? 2000;
        var rewardPoints = settings?.WaterRewardPoints ?? 5;
        var beforeEvents = await _water.GetByDateAsync(_currentUser.FamilyId, date);
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

        if (created && rewardPoints > 0 && beforeTotal < goal && beforeTotal + request.AmountMl >= goal)
            await EnsureRewardAsync(date, rewardPoints);

        return Ok(await BuildDayAsync(date));
    }

    [HttpDelete("today/latest")]
    public async Task<IActionResult> UndoLatest()
    {
        var timezone = await _timezone.GetTimezoneAsync(_currentUser.FamilyId);
        var date = TimezoneHelper.GetOperationalDate(timezone, DateTime.UtcNow);
        var latest = await _water.GetLatestByDateAsync(_currentUser.FamilyId, date);
        if (latest is not null)
            await _water.DeleteAsync(_currentUser.FamilyId, latest.Id);

        var settings = await _settings.GetByUserIdAsync(_currentUser.FamilyId);
        var goal = settings?.WaterGoalMl ?? 2000;
        var remaining = await _water.GetByDateAsync(_currentUser.FamilyId, date);
        if (remaining.Sum(x => x.AmountMl) < goal)
            await EnsureReversalAsync(date);

        return Ok(await BuildDayAsync(date));
    }

    private async Task<object> BuildDayAsync(string date)
    {
        var events = await _water.GetByDateAsync(_currentUser.FamilyId, date);
        var settings = await _settings.GetByUserIdAsync(_currentUser.FamilyId);
        var goal = settings?.WaterGoalMl ?? 2000;
        var rewardPoints = settings?.WaterRewardPoints ?? 5;
        var total = events.Sum(x => x.AmountMl);
        var awardedPoints = await GetEffectiveRewardAsync(date);

        return new
        {
            date,
            goalMl = goal,
            totalMl = total,
            rewardPoints,
            rewarded = awardedPoints > 0,
            task = new
            {
                id = WaterTaskId(date),
                title = "Beber água",
                points = rewardPoints,
                isDone = total >= goal && awardedPoints > 0
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

    private static string WaterTaskId(string date) => $"water:{date}";

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
            "Beber água",
            PointTransactionType.Award,
            rewardPoints,
            _currentUser.UserId,
            _currentUser.Role,
            "Meta diária de hidratação concluída");
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
            "Beber água",
            PointTransactionType.Reversal,
            -effectiveReward,
            _currentUser.UserId,
            _currentUser.Role,
            "Meta diária de hidratação voltou a ficar incompleta");
    }

    public record AddWaterRequest(int AmountMl, string EventId);
}
