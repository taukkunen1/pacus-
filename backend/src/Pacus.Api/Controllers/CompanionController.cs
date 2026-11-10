using System.Security.Cryptography;
using System.Text;
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
[Route("api/v1/pacus/companions/turtle")]
public class CompanionController(
    IPacusRepository pacusRepository,
    IPointTransactionRepository points,
    IDailyRoutineRepository routines,
    IFamilyTimezoneService timezone,
    ICurrentUserService user) : ControllerBase
{
    private const int Cost = 100;

    // The debit is also the ownership record. A stable ID makes retries idempotent.
    private ObjectId PurchaseId => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"pacus:turtle:{user.FamilyId}"))[..12]);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var purchase = await points.GetByIdForFamilyAsync(user.FamilyId, PurchaseId);
        return Ok(await BuildResponseAsync(purchase));
    }

    [HttpPost]
    [RequireRole(UserRole.Adult)]
    public async Task<IActionResult> Purchase()
    {
        var id = PurchaseId;
        var purchase = await points.GetByIdForFamilyAsync(user.FamilyId, id);
        if (purchase is null)
        {
            if (await pacusRepository.GetByFamilyIdAsync(user.FamilyId) is null)
                return NotFound();

            var familyTimezone = await timezone.GetTimezoneAsync(user.FamilyId);
            purchase = await points.CreateAsync(new PointTransaction
            {
                Id = id,
                FamilyId = user.FamilyId,
                Date = TimezoneHelper.GetOperationalDate(familyTimezone),
                TaskId = id.ToString(),
                TaskTitle = "Ovo de tartaruga",
                Type = PointTransactionType.Redemption,
                SourceType = "pet",
                SourceId = id.ToString(),
                Points = -Cost,
                ActorId = user.UserId,
                ActorRole = user.Role,
                CreatedAt = DateTime.UtcNow,
            });
        }

        return Ok(await BuildResponseAsync(purchase));
    }

    private async Task<object> BuildResponseAsync(PointTransaction? purchase)
    {
        var balance = await points.GetBalanceAsync(user.FamilyId);
        if (purchase is null)
            return new { cost = Cost, balance, animal = (object?)null };

        var (_, count) = await routines.GetHistoryAsync(
            user.FamilyId, purchase.Date, null, 1, 1);
        var days = (int)count;
        var stage = DayClosingService.DetermineStageFromClosedDays(days);
        return new
        {
            cost = Cost,
            balance,
            animal = new
            {
                birthDate = purchase.Date,
                stage = stage.ToString(),
                totalClosedDays = days,
                size = DayClosingService.ComputeSize(stage, days),
            },
        };
    }
}
