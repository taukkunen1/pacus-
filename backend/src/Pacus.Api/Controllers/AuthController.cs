using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pacus.Application.DTOs;
using Pacus.Application.Services;

namespace Pacus.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [AllowAnonymous]
    [HttpPost("adult/login")]
    public async Task<IActionResult> AdultLogin([FromBody] AdultLoginRequest request)
    {
        try
        {
            var result = await _authService.AdultLoginAsync(request.Email, request.Password);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("child/login")]
    public async Task<IActionResult> ChildLogin([FromBody] ChildLoginRequest request)
    {
        try
        {
            var result = await _authService.ChildLoginAsync(request.UserId, request.Pin);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }
}
