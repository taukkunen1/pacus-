using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pacus/me/habitat")]
public class HabitatController : ControllerBase
{
    private readonly IHabitatRepository _habitatRepository;
    private readonly ICurrentUserService _currentUser;

    public HabitatController(
        IHabitatRepository habitatRepository,
        ICurrentUserService currentUser)
    {
        _habitatRepository = habitatRepository;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var habitat = await _habitatRepository.GetByFamilyIdAsync(
            _currentUser.FamilyId);

        if (habitat is null)
        {
            habitat = new Habitat
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                FamilyId = _currentUser.FamilyId,
                Elements = new HabitatElements(),
                Bounds = new HabitatBounds(),
                Theme = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            habitat = await _habitatRepository.UpsertAsync(habitat);
        }

        return Ok(habitat);
    }

    [RequireRole(UserRole.Adult)]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] Habitat request)
    {
        if (request.Bounds.Width <= 0 || request.Bounds.Height <= 0)
        {
            return BadRequest(new
            {
                error = "Os limites do habitat devem possuir largura e altura maiores que zero."
            });
        }

        var existing = await _habitatRepository.GetByFamilyIdAsync(
            _currentUser.FamilyId);

        var habitat = new Habitat
        {
            Id = existing?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            Elements = request.Elements ?? new HabitatElements(),
            Bounds = request.Bounds ?? new HabitatBounds(),
            Theme = request.Theme,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _habitatRepository.UpsertAsync(habitat);

        return Ok(habitat);
    }
}
