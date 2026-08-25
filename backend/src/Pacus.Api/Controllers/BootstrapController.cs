using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Application.DTOs;
using Pacus.Application.Services;

namespace Pacus.Api.Controllers;

[ApiController]
[Route("api/v1/bootstrap")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;

    public BootstrapController(IBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateInitialFamily(
        [FromBody] BootstrapRequest request)
    {
        try
        {
            var result =
                await _bootstrapService.CreateInitialFamilyAsync(request);

            return Created("", result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                error = ex.Message
            });
        }
    }
}