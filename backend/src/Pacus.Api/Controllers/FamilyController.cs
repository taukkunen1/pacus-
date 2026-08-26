using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

// Autorizado para qualquer papel (adulto ou crianca) — so devolve nome + id,
// nunca PIN/senha, entao nao ha problema em cachear isso no frontend.
[ApiController]
[Authorize]
[Route("api/v1/family")]
public class FamilyController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public FamilyController(IUserRepository userRepository, ICurrentUserService currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    // Usado pela tela de login para trocar o campo "cole o id do perfil" por uma
    // lista com o nome de cada crianca da familia.
    [HttpGet("children")]
    public async Task<IActionResult> GetChildren()
    {
        var children = await _userRepository.GetByFamilyAndRoleAsync(_currentUser.FamilyId, UserRole.Child);

        var result = children
            .Select(c => new ChildProfileDto(c.Id.ToString(), c.Name))
            .ToList();

        return Ok(result);
    }
}
