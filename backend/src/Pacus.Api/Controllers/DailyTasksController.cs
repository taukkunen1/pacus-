using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Application.Services;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/daily-tasks")]
public class DailyTasksController : ControllerBase
{
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly ICurrentUserService _currentUser;

    public DailyTasksController(
        IDailyRoutineService dailyRoutineService,
        ICurrentUserService currentUser)
    {
        _dailyRoutineService = dailyRoutineService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request)
    {
        try
        {
            var routine =
                await _dailyRoutineService.CreateAdHocTaskAsync(
                    _currentUser.FamilyId,
                    request,
                    _currentUser.UserId,
                    _currentUser.Role.ToString());

            return Ok(routine);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(string id)
    {
        var routine =
            await _dailyRoutineService.ToggleTaskAsync(
                _currentUser.FamilyId,
                id,
                true,
                _currentUser.UserId,
                _currentUser.Role.ToString());

        return Ok(routine);
    }

    [HttpPost("{id}/reopen")]
    public async Task<IActionResult> Reopen(string id)
    {
        var routine =
            await _dailyRoutineService.ToggleTaskAsync(
                _currentUser.FamilyId,
                id,
                false,
                _currentUser.UserId,
                _currentUser.Role.ToString());

        return Ok(routine);
    }

    [HttpPut("{id}/points")]
    public async Task<IActionResult> AdjustPoints(
        string id,
        [FromBody] AdjustPointsRequest request)
    {
        try
        {
            var routine =
                await _dailyRoutineService.AdjustTaskPointsAsync(
                    _currentUser.FamilyId,
                    id,
                    request.Points,
                    _currentUser.UserId,
                    _currentUser.Role.ToString());

            return Ok(routine);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] DailyTaskUpdateRequest request)
    {
        try
        {
            var routine =
                await _dailyRoutineService.UpdateTaskAsync(
                    _currentUser.FamilyId,
                    id,
                    request,
                    _currentUser.UserId,
                    _currentUser.Role.ToString());

            return Ok(routine);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var routine =
                await _dailyRoutineService.DeleteTaskAsync(
                    _currentUser.FamilyId,
                    id,
                    _currentUser.UserId,
                    _currentUser.Role.ToString());

            return Ok(routine);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
