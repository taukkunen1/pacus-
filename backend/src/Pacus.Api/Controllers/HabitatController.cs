using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
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

    // Revisao de API (2026-09-11, achado #5): antes recebia a entidade de dominio
    // Habitat crua ([FromBody] Habitat) -- os campos sensiveis (Id/FamilyId/
    // CreatedAt) ja eram reconstruidos no servidor logo abaixo, mas Theme passava
    // direto sem nenhuma validacao. UpdateHabitatRequest expõe so os campos que o
    // cliente realmente define.
    [RequireRole(UserRole.Adult)]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateHabitatRequest request)
    {
        // Bounds ausente equivale ao antigo comportamento do binding direto na
        // entidade (Habitat.Bounds nunca era null, so zerado) -- mantido aqui
        // como invalido em vez de silenciosamente virar 0x0.
        if (request.Bounds is null || request.Bounds.Width <= 0 || request.Bounds.Height <= 0)
        {
            return BadRequest(new
            {
                error = "Os limites do habitat devem possuir largura e altura maiores que zero."
            });
        }

        if (request.Theme is not null && request.Theme.Length > 50)
        {
            return BadRequest(new { error = "O tema do habitat deve ter no maximo 50 caracteres." });
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
