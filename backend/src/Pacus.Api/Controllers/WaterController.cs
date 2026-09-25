using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Application.Utils;
using Pacus.Domain.Entities;

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

    public WaterController(IWaterIntakeRepository water, ISettingsRepository settings,
        IFamilyTimezoneService timezone, ICurrentUserService currentUser)
    {
        _water = water;
        _settings = settings;
        _timezone = timezone;
        _currentUser = currentUser;
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
        try { await _water.CreateAsync(intake); }
        catch (MongoDB.Driver.MongoWriteException ex) when (
            ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            // Retry/offline sync do mesmo evento: idempotente.
        }
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
        return Ok(await BuildDayAsync(date));
    }

    private async Task<object> BuildDayAsync(string date)
    {
        var events = await _water.GetByDateAsync(_currentUser.FamilyId, date);
        var settings = await _settings.GetByUserIdAsync(_currentUser.FamilyId);
        var configuredGoal = settings?.WaterGoalMl ?? 1000;
        var goal = configuredGoal is 500 or 800 or 1000 ? configuredGoal : 1000;
        return new
        {
            date,
            goalMl = goal,
            totalMl = events.Sum(x => x.AmountMl),
            entries = events.Select(x => new
            {
                id = x.Id.ToString(),
                amountMl = x.AmountMl,
                occurredAt = x.OccurredAt,
                actorId = x.ActorId.ToString()
            })
        };
    }

    public record AddWaterRequest(int AmountMl, string EventId);
}
